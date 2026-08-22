using System;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Cosmos.Models;
using Xunit;
using CosmosPartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Models;

public class PartitionKeyTests
{
    [Fact]
    public void Constructor_ShouldMapStringToAStringPartitionKey()
    {
        // Arrange & Act
        var partitionKey = new PartitionKey<string>("tenant-a");

        // Assert
        partitionKey.CosmosPartitionKey.ShouldBe(new CosmosPartitionKey("tenant-a"));
    }

    [Fact]
    public void Constructor_ShouldMapDoubleToANumericPartitionKey()
    {
        // Arrange & Act
        var partitionKey = new PartitionKey<double>(42.5);

        // Assert: must NOT fall back to ToString(), Cosmos treats "42.5" and 42.5 as different keys
        partitionKey.CosmosPartitionKey.ShouldBe(new CosmosPartitionKey(42.5));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Constructor_ShouldMapBoolToABooleanPartitionKey(bool value)
    {
        // Arrange & Act
        var partitionKey = new PartitionKey<bool>(value);

        // Assert
        partitionKey.CosmosPartitionKey.ShouldBe(new CosmosPartitionKey(value));
    }

    [Fact]
    public void Constructor_ShouldMapOtherTypesUsingToString()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var partitionKey = new PartitionKey<Guid>(id);

        // Assert
        partitionKey.CosmosPartitionKey.ShouldBe(new CosmosPartitionKey(id.ToString()));
    }

    [Fact]
    public void Constructor_ShouldThrowForNull()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new PartitionKey<string?>(null));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
    }

    [Fact]
    public void Constructor_ShouldMapAnEmptyStringToAnEmptyStringPartitionKey()
    {
        // Arrange & Act: an empty string is a valid (if unusual) partition key and must not throw
        var partitionKey = new PartitionKey<string>(string.Empty);

        // Assert
        partitionKey.CosmosPartitionKey.ShouldBe(new CosmosPartitionKey(string.Empty));
    }
}
