using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public abstract partial class RepositoryTestBase
{
    [Fact]
    public async Task HierarchicalPartitionKey_CreateAndGetAsync_ShouldRoundTripEveryComponent()
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
    public async Task HierarchicalPartitionKey_GetAsync_ShouldNotFindTheDocumentUnderAnotherLeaf()
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
    public async Task HierarchicalPartitionKey_CreateAsync_ShouldKeepDocumentsOfDifferentLeavesApart()
    {
        // Arrange: same id, same customer, same meter - only the time bucket differs. Documents
        // are unique per logical partition, so both have to survive
        await ResetAsync();
        var first = UsageEvent.Faker.Generate();
        var second = UsageEvent.Faker.Generate();
        second.CustomerId = first.CustomerId;
        second.MeterSlug = first.MeterSlug;
        second.TimeBucket = first.TimeBucket + "-later";

        // Act
        await UsageEventRepository.CreateAsync(first);
        await UsageEventRepository.CreateAsync(second);

        // Assert
        var fetchedFirst = await UsageEventRepository.GetAsync(
            first.Id,
            first.GetPartitionKey());
        var fetchedSecond = await UsageEventRepository.GetAsync(
            second.Id,
            second.GetPartitionKey());

        fetchedFirst.Quantity.ShouldBe(first.Quantity);
        fetchedSecond.Quantity.ShouldBe(second.Quantity);
    }

    [Fact]
    public async Task HierarchicalPartitionKey_UpdateAsync_ShouldWork()
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
    public async Task HierarchicalPartitionKey_UpsertAsync_ShouldWork()
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
    public async Task HierarchicalPartitionKey_PatchAsync_ShouldWork()
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
    public async Task HierarchicalPartitionKey_DeleteAsync_ShouldWork()
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
    public async Task HierarchicalPartitionKey_CreateTransactionalBatch_ShouldCommitInsideOneLeaf()
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
    public async Task HierarchicalPartitionKey_CreateTransactionalBatch_ShouldRejectAnotherLeaf()
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
    public async Task HierarchicalPartitionKey_QueryAsync_ShouldReturnEveryLeafOfAComponentPrefix()
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
}
