using Microsoft.Azure.Cosmos;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;
using Xunit;
using CosmosPartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Extensions;

public class PartitionKeyValueExtensionsTests
{
    [Fact]
    public void ToCosmosPartitionKey_ShouldMapASingleComponentToAPlainPartitionKey()
    {
        // Arrange
        var partitionKey = new PartitionKeyValue("acme");

        // Act
        var cosmosPartitionKey = partitionKey.ToCosmosPartitionKey();

        // Assert: unchanged from what a single-value key has always produced
        cosmosPartitionKey.ShouldBe(new CosmosPartitionKey("acme"));
    }

    [Fact]
    public void ToCosmosPartitionKey_ShouldMapAHierarchyThroughThePartitionKeyBuilder()
    {
        // Arrange
        var partitionKey = new PartitionKeyValue("cust-1", "api-calls", "2026-08");

        // Act
        var cosmosPartitionKey = partitionKey.ToCosmosPartitionKey();

        // Assert
        var expected = new PartitionKeyBuilder()
            .Add("cust-1")
            .Add("api-calls")
            .Add("2026-08")
            .Build();

        cosmosPartitionKey.ShouldBe(expected);
    }

    [Fact]
    public void ToCosmosPartitionKey_ShouldNotFlattenTheComponents()
    {
        // Arrange & Act: a joined key addresses a container with a single path, a hierarchical one
        // addresses a container with two - they must not translate to the same value
        var joined = new PartitionKeyValue("cust-1/api-calls").ToCosmosPartitionKey();
        var hierarchical = new PartitionKeyValue("cust-1", "api-calls").ToCosmosPartitionKey();

        // Assert
        joined.ShouldNotBe(hierarchical);
    }
}
