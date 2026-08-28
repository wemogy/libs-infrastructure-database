using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.DynamicProxies;
using Wemogy.Core.DynamicProxies.Enums;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task PatchAsync_ShouldSetASingleField()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act
        await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Set(x => x.Firstname, "Patched"));

        // Assert: the patched field changed, every other field is untouched
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Firstname.ShouldBe("Patched");
        persistedUser.Lastname.ShouldBe(user.Lastname);
        persistedUser.TenantId.ShouldBe(user.TenantId);
        persistedUser.Credits.ShouldBe(user.Credits);
    }

    [Fact]
    public async Task PatchAsync_ShouldIncrementAndDecrement()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Credits = 10;
        user.Score = 1.5;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p
                .Increment(x => x.Credits, 5)
                .Increment(x => x.Score, 0.25));
        var decrementedUser = await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p
                .Increment(x => x.Credits, -3)
                .Increment(x => x.Score, -0.5));

        // Assert
        decrementedUser.Credits.ShouldBe(12);
        decrementedUser.Score.ShouldBe(1.25);
    }

    [Fact]
    public async Task PatchAsync_ShouldCreateTheFieldWhenItIsAbsent()
    {
        // Arrange: Credits is not part of the faker, so the document carries its default
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        user.Credits.ShouldBe(0);

        // Act
        var patchedUser = await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Increment(x => x.Credits, 7));

        // Assert: the increment starts at zero instead of failing
        patchedUser.Credits.ShouldBe(7);
    }

    [Fact]
    public async Task PatchAsync_ShouldApplySeveralOperationsAtOnce()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act
        await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p
                .Set(x => x.Firstname, "Patched")
                .Set(x => x.Lastname, "Twice")
                .Increment(x => x.Credits, 3));

        // Assert
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Firstname.ShouldBe("Patched");
        persistedUser.Lastname.ShouldBe("Twice");
        persistedUser.Credits.ShouldBe(3);
    }

    [Fact]
    public async Task PatchAsync_ShouldReturnThePatchedEntity()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act
        var patchedUser = await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p
                .Set(x => x.Firstname, "Returned")
                .Increment(x => x.Credits, 2));

        // Assert: the caller does not have to re-read to see the new state
        patchedUser.Id.ShouldBe(user.Id);
        patchedUser.Firstname.ShouldBe("Returned");
        patchedUser.Credits.ShouldBe(2);
        patchedUser.Lastname.ShouldBe(user.Lastname);
    }

    [Fact]
    public async Task PatchAsync_ShouldApplyWhenTheConditionHolds()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Credits = 4;
        user.CreditsCap = 10;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act: a condition compares fields and constants; the database does not evaluate arithmetic
        // on document fields, so a delta belongs on the constant side of the comparison
        var patchedUser = await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Increment(x => x.Credits, 5),
            x => x.Credits < x.CreditsCap);

        // Assert
        patchedUser.Credits.ShouldBe(9);
    }

    [Fact]
    public async Task PatchAsync_ShouldThrowAndChangeNothingWhenTheConditionFails()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Credits = 8;
        user.CreditsCap = 10;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var exception = await Should.ThrowAsync<ConflictErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p => p
                    .Increment(x => x.Credits, 5)
                    .Set(x => x.Firstname, "Denied"),
                x => x.Credits > x.CreditsCap));

        // Assert: a failed condition is a conflict, and not a single operation was applied
        exception.Code.ShouldBe("PatchConditionNotMet");
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Credits.ShouldBe(8);
        persistedUser.Firstname.ShouldBe(user.Firstname);
    }

    [Fact]
    public async Task PatchAsync_ShouldNotRetryAFailedCondition()
    {
        // Arrange: the failure a failed condition produces, injected into the client. If it were
        // mapped to PreconditionFailedErrorException, the retry proxy of every repository would
        // burn three more attempts and a backoff on a deterministic answer
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);

        var flakyProxy = new FlakyProxy(
                100,
                FlakyStrategy.ThrowBeforeInvocation,
                () => PatchError.ConditionNotMet(
                    user.Id,
                    partitionKey))
            .OnlyForMethodsWithName(nameof(IDatabaseClient<User>.PatchAsync));
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = flakyProxy;
        var flakyUserRepository = UserRepositoryFactory();

        // the proxy is baked into the client of the repository above, so it is cleared right away:
        // the test base only resets it after it has built its repositories, which would hand the
        // proxy to the next test class as well
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
        await flakyUserRepository.CreateAsync(user);

        // Act
        await Should.ThrowAsync<ConflictErrorException>(
            () => flakyUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p => p.Increment(x => x.Credits, 1),
                x => x.Credits < 0));

        // Assert: one attempt, not four
        flakyProxy.FailAttempts.ShouldBe(1);
    }

    [Fact]
    public async Task PatchAsync_ShouldThrowNotFoundForAMissingDocument()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var missingId = Guid.NewGuid().ToString();

        // Act & Assert: a missing document is not reported as a failed condition, with or without
        // a condition - the two mean different things to the caller
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                missingId,
                partitionKey,
                p => p.Increment(x => x.Credits, 1)));

        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                missingId,
                partitionKey,
                p => p.Increment(x => x.Credits, 1),
                x => x.Credits >= 0));
    }

    [Fact]
    public async Task PatchAsync_ShouldRejectAPathThatIsNotAMemberAccess()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act & Assert: rejected while the operations are collected, before any I/O
        var exception = await Should.ThrowAsync<UnexpectedErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p => p.Set(x => x.Firstname.ToUpper(), "Patched")));
        exception.Code.ShouldBe("PatchPathNotSupported");

        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Firstname.ShouldBe(user.Firstname);
    }

    [Fact]
    public async Task PatchAsync_ShouldRejectPatchingIdPartitionKeyOrETag()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        var refusedOperations = new Action<IPatchOperations<User>>[]
        {
            p => p.Set(x => x.Id, Guid.NewGuid().ToString()),
            p => p.Set(x => x.TenantId, NewPartitionKey()),
            p => p.Set(x => x.ETag, "\"stolen\"")
        };

        // Act & Assert
        foreach (var operations in refusedOperations)
        {
            var exception = await Should.ThrowAsync<UnexpectedErrorException>(
                () => MicrosoftUserRepository.PatchAsync(
                    user.Id,
                    partitionKey,
                    operations));
            exception.Code.ShouldBe("PatchPathNotAllowed");
        }
    }

    [Fact]
    public async Task PatchAsync_ShouldRejectAnEmptyPatch()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act & Assert: unlike an empty batch, an empty patch is always a mistake
        var exception = await Should.ThrowAsync<UnexpectedErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p => { }));
        exception.Code.ShouldBe("PatchIsEmpty");
    }

    [Fact]
    public async Task PatchAsync_ShouldRejectMoreThanTenOperations()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act: the cap itself is fine
        var patchedUser = await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p =>
            {
                for (var i = 0; i < PatchOperationsBuilder<User>.MaxOperationCount; i++)
                {
                    p.Increment(x => x.Credits, 1);
                }
            });

        // Assert
        patchedUser.Credits.ShouldBe(PatchOperationsBuilder<User>.MaxOperationCount);

        var exception = await Should.ThrowAsync<UnexpectedErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p =>
                {
                    for (var i = 0; i <= PatchOperationsBuilder<User>.MaxOperationCount; i++)
                    {
                        p.Increment(x => x.Credits, 1);
                    }
                }));
        exception.Code.ShouldBe("PatchOperationLimitExceeded");
    }

    [Fact]
    public async Task PatchAsync_ShouldResolveTheJsonPropertyName()
    {
        // Arrange: Label is serialized as "customLabel", so a hand-rolled camelCase path would
        // write a field the entity does not read back from
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act
        await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Set(x => x.Label, "Labelled"));

        // Assert
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Label.ShouldBe("Labelled");
    }

    [Fact]
    public async Task PatchAsync_ShouldNeverExceedTheCapUnderConcurrency()
    {
        // Arrange: the test this feature exists for. 50 callers race to increment a balance that
        // must never pass its cap, and the condition is the only thing keeping them apart
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Credits = 0;
        user.CreditsCap = 10;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act: each attempt is handed to the thread pool rather than awaited where it is created.
        // The in-memory provider applies a patch synchronously and hands back a completed task, so
        // awaiting the calls in sequence ran all fifty one after another and the cap held without
        // anything ever racing for it
        var results = await Task.WhenAll(
            Enumerable.Range(0, 50)
                .Select(
                    _ => Task.Run(
                        async () =>
                        {
                            try
                            {
                                await MicrosoftUserRepository.PatchAsync(
                                    user.Id,
                                    partitionKey,
                                    p => p.Increment(x => x.Credits, 1),
                                    x => x.Credits < x.CreditsCap);
                                return true;
                            }
                            catch (ConflictErrorException)
                            {
                                return false;
                            }
                        })));

        // Assert: exactly the cap was granted, and the stored balance agrees
        results.Count(x => x).ShouldBe(10);
        results.Count(x => !x).ShouldBe(40);

        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Credits.ShouldBe(10);
    }

    [Fact]
    public async Task TransactionalBatch_ShouldPatchAtomicallyWithACreate()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var userToPatch = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        var userToCreate = NewUser(partitionKey);

        // Act: the event and the balance it advances commit together
        await MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Create(userToCreate)
            .Patch(
                userToPatch.Id,
                p => p.Increment(x => x.Credits, 5))
            .ExecuteAsync();

        // Assert
        var patchedUser = await MicrosoftUserRepository.GetAsync(
            userToPatch.Id,
            partitionKey);
        patchedUser.Credits.ShouldBe(5);

        var createdUser = await MicrosoftUserRepository.GetAsync(
            userToCreate.Id,
            partitionKey);
        createdUser.Firstname.ShouldBe(userToCreate.Firstname);
    }

    [Fact]
    public async Task TransactionalBatch_ShouldRollBackWhenThePatchConditionFails()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var userToPatch = NewUser(partitionKey);
        userToPatch.Credits = 8;
        userToPatch.CreditsCap = 10;
        await MicrosoftUserRepository.CreateAsync(userToPatch);
        var userToCreate = NewUser(partitionKey);

        var batch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Create(userToCreate)
            .Patch(
                userToPatch.Id,
                p => p.Increment(x => x.Credits, 5),
                x => x.Credits > x.CreditsCap);

        // Act
        var exception = await Should.ThrowAsync<ConflictErrorException>(() => batch.ExecuteAsync());

        // Assert: the sibling create is rolled back with the patch
        exception.Code.ShouldBe("PatchConditionNotMet");
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(
                userToCreate.Id,
                partitionKey));

        var persistedUser = await MicrosoftUserRepository.GetAsync(
            userToPatch.Id,
            partitionKey);
        persistedUser.Credits.ShouldBe(8);
    }

    [Fact]
    public async Task TransactionalBatch_ShouldDistinguishConditionFailureFromETagMismatch()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        var staleUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        var freshUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        freshUser.Firstname = "Fresh";
        await MicrosoftUserRepository.ReplaceAsync(freshUser);

        var patchBatch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Patch(
                user.Id,
                p => p.Increment(x => x.Credits, 1),
                x => x.Credits < 0);
        var replaceBatch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Replace(staleUser);

        // Act
        var conditionException = await Record.ExceptionAsync(() => patchBatch.ExecuteAsync());
        var eTagException = await Record.ExceptionAsync(() => replaceBatch.ExecuteAsync());

        // Assert: both answer a 412, and they still stay apart - "the state does not permit this"
        // is not the same as "someone else changed this"
        conditionException.ShouldBeOfType<ConflictErrorException>();
        eTagException.ShouldBeOfType<PreconditionFailedErrorException>();
    }
}
