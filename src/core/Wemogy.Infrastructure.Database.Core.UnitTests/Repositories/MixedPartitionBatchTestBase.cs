using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

/// <summary>
///     The behaviour every provider owes a mixed-type partition batch: writing documents of
///     different shapes into one logical partition of one container atomically. Kept apart from
///     <see cref="RepositoryTestBase"/> for the reason <see cref="HierarchicalPartitionKeyTestBase"/>
///     is - it needs its own repositories, and the Cosmos emulator does not survive one more client
///     per test in the shared base.
///     <para>
///         The batch is created from the <see cref="UsageEvent"/> repository, and the
///         <see cref="QuotaBalance"/> written through it is read back through its own repository -
///         which, for Cosmos, is mapped to the same container, and for the in-memory provider shares
///         the same static store.
///     </para>
/// </summary>
public abstract class MixedPartitionBatchTestBase
{
    protected MixedPartitionBatchTestBase(
        Func<IDatabaseRepository<UsageEvent>> usageEventRepositoryFactory,
        Func<IDatabaseRepository<QuotaBalance>> quotaBalanceRepositoryFactory)
    {
        // cleared before the repositories are built, for the reason RepositoryTestBase clears it
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;

        UsageEventRepository = usageEventRepositoryFactory();
        QuotaBalanceRepository = quotaBalanceRepositoryFactory();
    }

    protected IDatabaseRepository<UsageEvent> UsageEventRepository { get; }

    protected IDatabaseRepository<QuotaBalance> QuotaBalanceRepository { get; }

    protected virtual async Task ResetAsync()
    {
        await UsageEventRepository.DeleteAsync(x => true);
        await QuotaBalanceRepository.DeleteAsync(x => true);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldWriteBothTypesAtomically()
    {
        // Arrange: a balance already exists, and a consume both records an event and moves the
        // balance - two shapes, one partition, one batch
        await ResetAsync();
        var balance = NewBalance();
        balance.Consumed = 3m;
        await QuotaBalanceRepository.CreateAsync(balance);

        var usageEvent = NewEventFor(balance);

        // Act
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        batch.Create(usageEvent);
        batch.Patch<QuotaBalance>(
            balance.Id,
            p => p.Increment(x => x.Consumed, 1m));
        await batch.ExecuteAsync();

        // Assert: both writes landed
        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(4m);

        var fetchedEvent = await UsageEventRepository.GetAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        fetchedEvent.ShouldNotBeNull();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRollBackBothTypesWhenThePatchConditionFails()
    {
        // Arrange: the balance is already at the cap, so the conditional increment cannot apply
        await ResetAsync();
        var balance = NewBalance();
        balance.Consumed = 10m;
        await QuotaBalanceRepository.CreateAsync(balance);

        var usageEvent = NewEventFor(balance);

        // Act: the event create would succeed on its own, but the failing patch condition has to
        // roll the whole batch back - across the type boundary
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        batch.Create(usageEvent);
        batch.Patch<QuotaBalance>(
            balance.Id,
            p => p.Increment(x => x.Consumed, 1m),
            x => x.Consumed < 10m);

        var exception = await Record.ExceptionAsync(() => batch.ExecuteAsync());

        // Assert: the failure names the unmet condition, and neither write survived
        exception.ShouldBeOfType<ConflictErrorException>();
        ((ConflictErrorException)exception).Code.ShouldBe("PatchConditionNotMet");

        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(10m);

        var eventExists = await UsageEventRepository.ExistsAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        eventExists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRollBackWhenACreateConflicts()
    {
        // Arrange: the event was already recorded, so replaying it is a conflict - the idempotency
        // case the create guards against. Its code has to differ from the failed-condition case, so
        // a caller can tell "already counted" from "cap reached"
        await ResetAsync();
        var balance = NewBalance();
        balance.Consumed = 2m;
        await QuotaBalanceRepository.CreateAsync(balance);

        var usageEvent = NewEventFor(balance);
        await UsageEventRepository.CreateAsync(usageEvent);

        // Act
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        batch.Create(usageEvent);
        batch.Patch<QuotaBalance>(
            balance.Id,
            p => p.Increment(x => x.Consumed, 1m),
            x => x.Consumed < 10m);

        var exception = await Record.ExceptionAsync(() => batch.ExecuteAsync());

        // Assert: reported as a conflict, distinct from the unmet-condition code, and the balance
        // did not move
        exception.ShouldBeOfType<ConflictErrorException>();
        ((ConflictErrorException)exception).Code.ShouldBe("AlreadyExists");

        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(2m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRollBackWhenAReplaceCarriesAStaleETag()
    {
        // Arrange
        await ResetAsync();
        var balance = NewBalance();
        await QuotaBalanceRepository.CreateAsync(balance);

        // two reads of the same balance carry the same eTag; replacing through one of them bumps
        // the stored eTag, so the other now holds a stale one - a real stale eTag, not a fabricated
        // string the database would refuse to compare
        var staleBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        var freshBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        freshBalance.Consumed = 5m;
        await QuotaBalanceRepository.ReplaceAsync(freshBalance);

        staleBalance.Consumed = 9m;
        var usageEvent = NewEventFor(balance);

        // Act
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        batch.Create(usageEvent);
        batch.Replace(staleBalance);

        var exception = await Record.ExceptionAsync(() => batch.ExecuteAsync());

        // Assert: the stale replace is reported as a precondition failure, distinct from an unmet
        // patch condition, and neither write survived
        exception.ShouldBeOfType<PreconditionFailedErrorException>();
        ((PreconditionFailedErrorException)exception).Code.ShouldBe("EtagMismatch");

        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(5m);

        var eventExists = await UsageEventRepository.ExistsAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        eventExists.ShouldBeFalse();
    }

    [Fact]
    public async Task Add_ShouldRefuseAnEntityOfAnotherPartition()
    {
        // Arrange
        await ResetAsync();
        var balance = NewBalance();

        // the same customer and meter, a different bucket - so it is a different logical partition,
        // which a batch bound to one partition must refuse even across types
        var otherLeafEvent = NewEventFor(balance);
        otherLeafEvent.TimeBucket = balance.TimeBucket + "-later";

        // Act: refused when the operation is added, before the batch is ever executed
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        var exception = Record.Exception(() => batch.Create(otherLeafEvent));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyMismatch");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldHoldTheCapUnderConcurrentConsumes()
    {
        // Arrange: one balance, a cap of ten, and fifty consumes racing for it. Each consume is a
        // batch that records a distinct event and conditionally increments the balance, so the cap
        // holds only if the condition and the increment commit together and in isolation
        await ResetAsync();
        const int cap = 10;
        const int attempts = 50;

        var balance = NewBalance();
        await QuotaBalanceRepository.CreateAsync(balance);

        // Act
        var consumedEvents = await Task.WhenAll(
            Enumerable.Range(0, attempts)
                .Select(_ => TryConsumeAsync(balance, cap)));
        var succeeded = consumedEvents.Where(usageEvent => usageEvent != null).ToList();

        // Assert: exactly the cap consumed, and the balance sits at the cap - the condition held
        // the line under contention
        succeeded.Count.ShouldBe(cap);

        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(cap);

        // every success left its event behind - counted by the ids that succeeded rather than by a
        // query, because both types share one container for Cosmos and a query over the events
        // would also return the balance that sits in it
        foreach (var usageEvent in succeeded)
        {
            var exists = await UsageEventRepository.ExistsAsync(
                usageEvent!.Id,
                usageEvent.GetPartitionKey());
            exists.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task ExecuteAsync_ShouldCountAReplayedEventOnce()
    {
        // Arrange: the same event submitted twice, standing in for a retried request
        await ResetAsync();
        var balance = NewBalance();
        await QuotaBalanceRepository.CreateAsync(balance);

        var usageEvent = NewEventFor(balance);

        // Act: the first consume records the event and moves the balance
        var firstConsumed = await TryConsumeAsync(balance, 10, usageEvent);

        // the second carries the same event id, so its create conflicts and rolls the balance back
        var secondConsumed = await TryConsumeAsync(balance, 10, usageEvent);

        // Assert: the replay did not count, so the balance moved once and the event is there once
        firstConsumed.ShouldNotBeNull();
        secondConsumed.ShouldBeNull();

        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(1m);

        var eventExists = await UsageEventRepository.ExistsAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        eventExists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldRefuseACreateWhoseIdAnotherTypeOfTheBatchAlreadyHolds()
    {
        // Arrange: an id is unique per logical partition of a container, not per entity type, so a
        // balance and an event of one partition cannot share one. The in-memory provider keeps a
        // store per type, so without a cross-type check it would accept what Cosmos answers 409 for
        await ResetAsync();
        var balance = NewBalance();
        await QuotaBalanceRepository.CreateAsync(balance);

        var usageEvent = NewEventFor(balance);

        // same partition as the balance, and the id the balance already occupies
        var collidingEvent = new UsageEvent
        {
            Id = balance.Id,
            CustomerId = balance.CustomerId,
            MeterSlug = balance.MeterSlug,
            TimeBucket = balance.TimeBucket,
            Quantity = 1
        };

        // Act: the batch touches both types, so the balance's store is one the create can be
        // checked against
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        batch.Patch<QuotaBalance>(
            balance.Id,
            p => p.Increment(x => x.Consumed, 1m));
        batch.Create(collidingEvent);

        var exception = await Record.ExceptionAsync(() => batch.ExecuteAsync());

        // Assert: reported as a conflict, and the patch of the same batch rolled back with it
        exception.ShouldBeOfType<ConflictErrorException>();
        ((ConflictErrorException)exception).Code.ShouldBe("AlreadyExists");

        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(0m);

        // the event that did not collide is untouched by the failure
        var eventExists = await UsageEventRepository.ExistsAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        eventExists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReportTheFirstFailingOperationNotTheFirstFailingType()
    {
        // Arrange: a batch where two operations of two types would both fail, and the type whose
        // operation fails *later* is the one the batch touches *first*. A provider that validates
        // per type instead of in the order the operations were added reports the wrong one
        await ResetAsync();
        var balance = NewBalance();
        await QuotaBalanceRepository.CreateAsync(balance);

        var usageEvent = NewEventFor(balance);
        await UsageEventRepository.CreateAsync(usageEvent);

        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());

        // sees QuotaBalance first, and succeeds
        batch.Upsert(balance);

        // fails: the event was already recorded
        batch.Create(usageEvent);

        // would also fail, on the type the batch saw first - a cap of zero cannot admit a consume
        batch.Patch<QuotaBalance>(
            balance.Id,
            p => p.Increment(x => x.Consumed, 1m),
            x => x.Consumed < 0m);

        // Act
        var exception = await Record.ExceptionAsync(() => batch.ExecuteAsync());

        // Assert: the conflict of the second operation, not the unmet condition of the third
        exception.ShouldBeOfType<ConflictErrorException>();
        ((ConflictErrorException)exception).Code.ShouldBe("AlreadyExists");
    }

    [Fact]
    public async Task ExecuteAsync_ShouldApplyDeleteAndUpsertAcrossTypes()
    {
        // Arrange: the two operations the atomic-write tests above do not reach, on two types -
        // retiring an event while the balance it moved is written back
        await ResetAsync();
        var balance = NewBalance();
        balance.Consumed = 2m;
        await QuotaBalanceRepository.CreateAsync(balance);

        var usageEvent = NewEventFor(balance);
        await UsageEventRepository.CreateAsync(usageEvent);

        balance.Consumed = 7m;

        // Act
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        batch.Delete<UsageEvent>(usageEvent.Id);
        batch.Upsert(balance);
        await batch.ExecuteAsync();

        // Assert: the delete addressed a type the batch was not created from, and the upsert
        // carried no precondition
        var eventExists = await UsageEventRepository.ExistsAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        eventExists.ShouldBeFalse();

        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(7m);
    }

    [Fact]
    public async Task Add_ShouldThrowWhenExceedingTheOperationLimit()
    {
        // Arrange
        await ResetAsync();
        var balance = NewBalance();
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());

        // Act: the batch is never executed, the cap is enforced client-side - and it counts every
        // operation of the batch, not one counter per type
        for (var i = 0; i < DatabasePartitionBatchBase.MaxOperationCount - 1; i++)
        {
            batch.Create(NewEventFor(balance));
        }

        batch.Patch<QuotaBalance>(
            balance.Id,
            p => p.Increment(x => x.Consumed, 1m));

        // Assert
        batch.OperationCount.ShouldBe(DatabasePartitionBatchBase.MaxOperationCount);
        Should.Throw<UnexpectedErrorException>(() => batch.Create(NewEventFor(balance)));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldThrowWhenExecutedTwice()
    {
        // Arrange
        await ResetAsync();
        var balance = NewBalance();
        await QuotaBalanceRepository.CreateAsync(balance);

        var usageEvent = NewEventFor(balance);

        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        batch.Create(usageEvent);
        batch.Patch<QuotaBalance>(
            balance.Id,
            p => p.Increment(x => x.Consumed, 1m));
        await batch.ExecuteAsync();

        // Act & Assert: a batch is single-use, replaying it would apply every write a second time
        await Should.ThrowAsync<UnexpectedErrorException>(() => batch.ExecuteAsync());
        Should.Throw<UnexpectedErrorException>(() => batch.Create(NewEventFor(balance)));
        batch.OperationCount.ShouldBe(2);

        // the replay was refused before it reached the database, so the increment landed once
        var fetchedBalance = await QuotaBalanceRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Consumed.ShouldBe(1m);
    }

    [Fact]
    public async Task ExecuteAsync_ShouldDoNothingWhenEmpty()
    {
        // Arrange
        await ResetAsync();
        var balance = NewBalance();
        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());

        // Act
        await batch.ExecuteAsync();

        // Assert: an empty batch completes without touching the database, but is still spent
        batch.OperationCount.ShouldBe(0);
        await Should.ThrowAsync<UnexpectedErrorException>(() => batch.ExecuteAsync());
    }

    private static QuotaBalance NewBalance()
    {
        var balance = QuotaBalance.Faker.Generate();
        balance.Consumed = 0m;
        return balance;
    }

    private static UsageEvent NewEventFor(QuotaBalance balance)
    {
        var usageEvent = UsageEvent.Faker.Generate();
        usageEvent.CustomerId = balance.CustomerId;
        usageEvent.MeterSlug = balance.MeterSlug;
        usageEvent.TimeBucket = balance.TimeBucket;
        return usageEvent;
    }

    private async Task<UsageEvent?> TryConsumeAsync(QuotaBalance balance, int cap, UsageEvent? usageEvent = null)
    {
        var eventToRecord = usageEvent ?? NewEventFor(balance);

        var batch = UsageEventRepository.CreatePartitionBatch(balance.GetPartitionKey());
        batch.Create(eventToRecord);
        batch.Patch<QuotaBalance>(
            balance.Id,
            p => p.Increment(x => x.Consumed, 1m),
            x => x.Consumed < cap);

        try
        {
            await batch.ExecuteAsync();
            return eventToRecord;
        }
        catch (ConflictErrorException)
        {
            // either the cap was reached (the condition did not hold) or the event was already
            // recorded (the create conflicted) - both roll the batch back, so nothing was consumed
            return null;
        }
    }
}
