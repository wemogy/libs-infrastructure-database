using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

/// <summary>
///     The behaviour every provider owes a hierarchical partition key. Kept apart from
///     <see cref="RepositoryTestBase"/> on purpose: these tests need a single repository, and
///     adding one to the shared base would build another database client for every test in it -
///     which the Cosmos emulator does not survive at that volume.
/// </summary>
public abstract class HierarchicalPartitionKeyTestBase
{
    protected HierarchicalPartitionKeyTestBase(Func<IDatabaseRepository<UsageEvent>> usageEventRepositoryFactory)
    {
        // cleared before the repository is built, for the reason RepositoryTestBase clears it
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;

        UsageEventRepository = usageEventRepositoryFactory();
    }

    protected IDatabaseRepository<UsageEvent> UsageEventRepository { get; }

    protected virtual Task ResetAsync()
    {
        return UsageEventRepository.DeleteAsync(x => true);
    }

    [Fact]
    public async Task CreateAndGetAsync_ShouldRoundTripEveryComponent()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();

        // Act
        await UsageEventRepository.CreateAsync(usageEvent);
        var fetchedUsageEvent = await UsageEventRepository.GetAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());

        // Assert
        fetchedUsageEvent.CustomerId.ShouldBe(usageEvent.CustomerId);
        fetchedUsageEvent.MeterSlug.ShouldBe(usageEvent.MeterSlug);
        fetchedUsageEvent.TimeBucket.ShouldBe(usageEvent.TimeBucket);
        fetchedUsageEvent.Quantity.ShouldBe(usageEvent.Quantity);
    }

    [Fact]
    public async Task GetAsync_ShouldNotFindTheDocumentUnderAnotherLeaf()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();
        await UsageEventRepository.CreateAsync(usageEvent);

        // Act: only the narrowest component differs, so this addresses a different logical
        // partition - which proves the whole hierarchy is sent, not just its head
        var exception = await Record.ExceptionAsync(
            () => UsageEventRepository.GetAsync(
                usageEvent.Id,
                new PartitionKeyValue(
                    usageEvent.CustomerId,
                    usageEvent.MeterSlug,
                    "another-bucket")));

        // Assert
        exception.ShouldBeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task CreateAsync_ShouldKeepDocumentsOfDifferentLeavesApart()
    {
        // Arrange: the same id, the same customer and the same meter - only the narrowest
        // component differs. A document is unique per logical partition, so both have to survive:
        // a provider that flattened the hierarchy would reject the second create as a conflict,
        // or overwrite the first
        await ResetAsync();
        var first = UsageEvent.Faker.Generate();
        var second = new UsageEvent
        {
            Id = first.Id,
            CustomerId = first.CustomerId,
            MeterSlug = first.MeterSlug,
            TimeBucket = first.TimeBucket + "-later",
            Quantity = first.Quantity + 1
        };

        // Act
        await UsageEventRepository.CreateAsync(first);
        await UsageEventRepository.CreateAsync(second);

        // Assert: each key reaches its own document, and neither overwrote the other
        var fetchedFirst = await UsageEventRepository.GetAsync(
            first.Id,
            first.GetPartitionKey());
        var fetchedSecond = await UsageEventRepository.GetAsync(
            second.Id,
            second.GetPartitionKey());

        fetchedFirst.Quantity.ShouldBe(first.Quantity);
        fetchedSecond.Quantity.ShouldBe(first.Quantity + 1);
    }

    [Fact]
    public async Task UpdateAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();
        await UsageEventRepository.CreateAsync(usageEvent);

        // Act
        var updatedUsageEvent = await UsageEventRepository.UpdateAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey(),
            x => x.Quantity = 4711);

        // Assert
        updatedUsageEvent.Quantity.ShouldBe(4711);

        var fetchedUsageEvent = await UsageEventRepository.GetAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        fetchedUsageEvent.Quantity.ShouldBe(4711);
    }

    [Fact]
    public async Task UpsertAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();

        // Act: an upsert that inserts, followed by one that updates
        await UsageEventRepository.UpsertAsync(usageEvent);
        usageEvent.Quantity = 99;
        await UsageEventRepository.UpsertAsync(usageEvent);

        // Assert
        var fetchedUsageEvent = await UsageEventRepository.GetAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        fetchedUsageEvent.Quantity.ShouldBe(99);
    }

    [Fact]
    public async Task PatchAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();
        usageEvent.Quantity = 10;
        await UsageEventRepository.CreateAsync(usageEvent);

        // Act
        var patchedUsageEvent = await UsageEventRepository.PatchAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey(),
            p => p.Increment(x => x.Quantity, 5));

        // Assert
        patchedUsageEvent.Quantity.ShouldBe(15);
    }

    [Fact]
    public async Task DeleteAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();
        await UsageEventRepository.CreateAsync(usageEvent);

        // Act
        await UsageEventRepository.DeleteAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());

        // Assert
        var exists = await UsageEventRepository.ExistsAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateTransactionalBatch_ShouldCommitInsideOneLeaf()
    {
        // Arrange: this is the case the feature exists for - a balance patch and a usage event
        // written atomically, while the store is still free to split the customer's tail
        await ResetAsync();
        var balance = UsageEvent.Faker.Generate();
        balance.Quantity = 100;
        await UsageEventRepository.CreateAsync(balance);

        var usageEvent = UsageEvent.Faker.Generate();
        usageEvent.CustomerId = balance.CustomerId;
        usageEvent.MeterSlug = balance.MeterSlug;
        usageEvent.TimeBucket = balance.TimeBucket;

        // Act
        var batch = UsageEventRepository.CreateTransactionalBatch(balance.GetPartitionKey());
        batch.Create(usageEvent);
        batch.Patch(
            balance.Id,
            p => p.Increment(x => x.Quantity, 7));
        await batch.ExecuteAsync();

        // Assert
        var fetchedBalance = await UsageEventRepository.GetAsync(
            balance.Id,
            balance.GetPartitionKey());
        fetchedBalance.Quantity.ShouldBe(107);

        var fetchedUsageEvent = await UsageEventRepository.GetAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        fetchedUsageEvent.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateTransactionalBatch_ShouldRejectAnotherLeaf()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();

        var otherLeaf = UsageEvent.Faker.Generate();
        otherLeaf.CustomerId = usageEvent.CustomerId;
        otherLeaf.MeterSlug = usageEvent.MeterSlug;
        otherLeaf.TimeBucket = usageEvent.TimeBucket + "-later";

        // Act: a batch is limited to one logical partition, and for a hierarchical key that means
        // the whole hierarchy has to match - not just the component the documents share
        var batch = UsageEventRepository.CreateTransactionalBatch(usageEvent.GetPartitionKey());
        var exception = Record.Exception(() => batch.Create(otherLeaf));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyMismatch");
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnEveryLeafOfAComponentPrefix()
    {
        // Arrange: the same customer, spread over two leaves
        await ResetAsync();
        var first = UsageEvent.Faker.Generate();
        var second = UsageEvent.Faker.Generate();
        second.CustomerId = first.CustomerId;
        second.MeterSlug = first.MeterSlug;
        second.TimeBucket = first.TimeBucket + "-later";

        await UsageEventRepository.CreateAsync(first);
        await UsageEventRepository.CreateAsync(second);

        // Act: a filter on the broadest component alone, which is how a prefix-scoped read is
        // expressed until the query side learns to scope itself to a partial key
        var usageEvents = await UsageEventRepository.QueryAsync(x => x.CustomerId == first.CustomerId);

        // Assert
        usageEvents.Count.ShouldBe(2);
        usageEvents.Select(x => x.Id).ShouldBe(
            new[] { first.Id, second.Id },
            ignoreOrder: true);
    }

    [Fact]
    public async Task GetAsync_ShouldRejectAKeyOfTheWrongDepth()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();
        await UsageEventRepository.CreateAsync(usageEvent);

        // Act: a string converts to a one-component key implicitly, so this compiles cleanly
        // against an entity partitioned by three - and would otherwise be reported as a plain
        // not-found, which sends the caller looking for a missing document
        var exception = await Record.ExceptionAsync(
            () => UsageEventRepository.GetAsync(usageEvent.Id, usageEvent.CustomerId));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyDepthMismatch");
    }

    [Fact]
    public async Task UpsertAsync_ShouldRejectAKeyOfTheWrongDepth()
    {
        // Arrange: the write path matters more than the read one - a document written under an
        // under-specified key lands in a partition no read path can address
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();

        // Act
        var exception = await Record.ExceptionAsync(
            () => UsageEventRepository.UpsertAsync(usageEvent, usageEvent.CustomerId));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyDepthMismatch");
    }

    [Fact]
    public async Task CreateTransactionalBatch_ShouldRejectAKeyOfTheWrongDepth()
    {
        // Arrange
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();

        // Act
        var exception = Record.Exception(
            () => UsageEventRepository.CreateTransactionalBatch(usageEvent.CustomerId));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyDepthMismatch");
    }

    [Fact]
    public async Task UpdateAsync_ShouldAwaitAnAsynchronousUpdateAction()
    {
        // Arrange: the mutation happens after an await, so an update action whose task is not
        // awaited applies it only after the entity has already been written
        await ResetAsync();
        var usageEvent = UsageEvent.Faker.Generate();
        await UsageEventRepository.CreateAsync(usageEvent);

        // Act
        await UsageEventRepository.UpdateAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey(),
            async x =>
            {
                await Task.Yield();
                x.Quantity = 8125;
            });

        // Assert
        var fetchedUsageEvent = await UsageEventRepository.GetAsync(
            usageEvent.Id,
            usageEvent.GetPartitionKey());
        fetchedUsageEvent.Quantity.ShouldBe(8125);
    }
}
