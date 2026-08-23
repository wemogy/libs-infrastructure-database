using System;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task TransactionalBatch_ShouldCreateMultipleEntitiesAtomically()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var firstUser = NewUser(partitionKey);
        var secondUser = NewUser(partitionKey);
        var thirdUser = NewUser(partitionKey);
        var batch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey);

        // Act
        await batch
            .Create(firstUser)
            .Create(secondUser)
            .Create(thirdUser)
            .ExecuteAsync();

        // Assert
        batch.OperationCount.ShouldBe(3);
        foreach (var user in new[] { firstUser, secondUser, thirdUser })
        {
            var persistedUser = await MicrosoftUserRepository.GetAsync(
                user.Id,
                partitionKey);
            persistedUser.Firstname.ShouldBe(user.Firstname);
        }
    }

    [Fact]
    public async Task TransactionalBatch_ShouldApplyMixedOperations()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var userToReplace = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        var userToDelete = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        var userToCreate = NewUser(partitionKey);
        var replacement = await MicrosoftUserRepository.GetAsync(
            userToReplace.Id,
            partitionKey);
        replacement.Firstname = "Replaced";

        // Act
        await MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Create(userToCreate)
            .Replace(replacement)
            .Delete(userToDelete.Id)
            .ExecuteAsync();

        // Assert
        var createdUser = await MicrosoftUserRepository.GetAsync(
            userToCreate.Id,
            partitionKey);
        createdUser.Firstname.ShouldBe(userToCreate.Firstname);

        var replacedUser = await MicrosoftUserRepository.GetAsync(
            userToReplace.Id,
            partitionKey);
        replacedUser.Firstname.ShouldBe("Replaced");

        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(
                userToDelete.Id,
                partitionKey));
    }

    [Fact]
    public async Task TransactionalBatch_ShouldRollBackEverythingWhenOneOperationFails()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var existingUser = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        var firstUser = NewUser(partitionKey);
        var secondUser = NewUser(partitionKey);
        var conflictingUser = NewUser(
            partitionKey,
            existingUser.Id);

        var batch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Create(firstUser)
            .Create(secondUser)
            .Create(conflictingUser);

        // Act
        await Should.ThrowAsync<ConflictErrorException>(() => batch.ExecuteAsync());

        // Assert: the two valid creates of the failed batch were not applied either
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(
                firstUser.Id,
                partitionKey));
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(
                secondUser.Id,
                partitionKey));
    }

    [Fact]
    public async Task TransactionalBatch_ShouldRollBackWhenReplacingAMissingEntity()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var userToCreate = NewUser(partitionKey);
        var missingUser = NewUser(partitionKey);

        var batch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Create(userToCreate)
            .Replace(missingUser);

        // Act
        await Should.ThrowAsync<NotFoundErrorException>(() => batch.ExecuteAsync());

        // Assert
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(
                userToCreate.Id,
                partitionKey));
    }

    [Fact]
    public async Task TransactionalBatch_ShouldThrowPreconditionFailedForStaleETag()
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

        staleUser.Firstname = "Stale";
        var otherUser = NewUser(partitionKey);

        var batch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Replace(staleUser)
            .Create(otherUser);

        // Act: the batch is not wrapped by the retry proxy, so the 412 surfaces directly
        await Should.ThrowAsync<PreconditionFailedErrorException>(() => batch.ExecuteAsync());

        // Assert: neither the stale replace nor the valid create of the same batch was applied
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Firstname.ShouldBe("Fresh");

        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(
                otherUser.Id,
                partitionKey));
    }

    [Fact]
    public async Task TransactionalBatch_ShouldThrowForEntityOfAnotherPartition()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var batch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey);
        var userOfAnotherPartition = NewUser(NewPartitionKey());

        // Act & Assert: it throws when the operation is added, not when the batch is executed
        Should.Throw<UnexpectedErrorException>(() => batch.Create(userOfAnotherPartition));
        Should.Throw<UnexpectedErrorException>(() => batch.Replace(userOfAnotherPartition));
        Should.Throw<UnexpectedErrorException>(() => batch.Upsert(userOfAnotherPartition));
        batch.OperationCount.ShouldBe(0);
    }

    [Fact]
    public async Task TransactionalBatch_ShouldThrowWhenExceedingTheOperationLimit()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var batch = MicrosoftUserRepository.CreateTransactionalBatch(partitionKey);

        // Act: the batch is never executed, the cap is enforced client-side
        for (var i = 0; i < DatabaseTransactionalBatchBase<User>.MaxOperationCount; i++)
        {
            batch.Create(NewUser(partitionKey));
        }

        // Assert
        batch.OperationCount.ShouldBe(DatabaseTransactionalBatchBase<User>.MaxOperationCount);
        Should.Throw<UnexpectedErrorException>(() => batch.Create(NewUser(partitionKey)));
    }

    [Fact]
    public async Task TransactionalBatch_ShouldDoNothingWhenEmpty()
    {
        // Arrange
        await ResetAsync();
        var batch = MicrosoftUserRepository.CreateTransactionalBatch(NewPartitionKey());

        // Act
        await batch.ExecuteAsync();

        // Assert
        batch.OperationCount.ShouldBe(0);
    }

    [Fact]
    public async Task TransactionalBatch_ShouldAllowCreateThenReplaceOfTheSameEntity()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        var replacement = NewUser(
            partitionKey,
            user.Id);
        replacement.Firstname = "Replaced";

        // Act: the operations are validated in order against the state at execute time, so the
        // replace sees the entity the create of the same batch added
        await MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Create(user)
            .Replace(replacement)
            .ExecuteAsync();

        // Assert
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Firstname.ShouldBe("Replaced");
    }

    [Fact]
    public async Task TransactionalBatch_ShouldUpsertExistingAndNewEntities()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var existingUser = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        var updatedUser = NewUser(
            partitionKey,
            existingUser.Id);
        updatedUser.Firstname = "Updated";
        var newUser = NewUser(partitionKey);

        // Act
        await MicrosoftUserRepository.CreateTransactionalBatch(partitionKey)
            .Upsert(updatedUser)
            .Upsert(newUser)
            .ExecuteAsync();

        // Assert
        var persistedExistingUser = await MicrosoftUserRepository.GetAsync(
            existingUser.Id,
            partitionKey);
        persistedExistingUser.Firstname.ShouldBe("Updated");

        var persistedNewUser = await MicrosoftUserRepository.GetAsync(
            newUser.Id,
            partitionKey);
        persistedNewUser.Firstname.ShouldBe(newUser.Firstname);
    }

    private static string NewPartitionKey()
    {
        return Guid.NewGuid().ToString();
    }

    private static User NewUser(string partitionKey, string? id = null)
    {
        var faker = User.Faker;

        if (id != null)
        {
            faker = faker.RuleFor(x => x.Id, id);
        }

        var user = faker.Generate();
        user.TenantId = partitionKey;
        return user;
    }
}
