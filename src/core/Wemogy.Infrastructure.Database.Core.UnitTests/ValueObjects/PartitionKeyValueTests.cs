using System.Collections.Generic;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.ValueObjects;

public class PartitionKeyValueTests
{
    [Fact]
    public void ImplicitConversion_ShouldBuildASingleComponentKey()
    {
        // Arrange & Act: this is what keeps every existing call site compiling unchanged
        PartitionKeyValue partitionKey = "acme";

        // Assert
        partitionKey.Count.ShouldBe(1);
        partitionKey.IsHierarchical.ShouldBeFalse();
        partitionKey[0].ShouldBe("acme");
    }

    [Fact]
    public void Constructor_ShouldKeepTheComponentsInTheGivenOrder()
    {
        // Arrange & Act
        var partitionKey = new PartitionKeyValue("cust-1", "api-calls", "2026-08");

        // Assert
        partitionKey.Count.ShouldBe(3);
        partitionKey.IsHierarchical.ShouldBeTrue();
        partitionKey.Components.ShouldBe(new[] { "cust-1", "api-calls", "2026-08" });
    }

    [Fact]
    public void ToString_ShouldReturnTheValueItselfForASingleComponent()
    {
        // Arrange & Act: the error messages embed the key, so their wording must not change for
        // the entities that have always had a single-value key
        var partitionKey = new PartitionKeyValue("acme");

        // Assert
        partitionKey.ToString().ShouldBe("acme");
    }

    [Fact]
    public void ToString_ShouldJoinTheComponentsOfAHierarchy()
    {
        // Arrange & Act
        var partitionKey = new PartitionKeyValue("cust-1", "api-calls");

        // Assert
        partitionKey.ToString().ShouldBe("cust-1/api-calls");
    }

    [Fact]
    public void Equals_ShouldCompareEveryComponent()
    {
        // Arrange & Act
        var partitionKey = new PartitionKeyValue("cust-1", "api-calls", "2026-08");
        var same = new PartitionKeyValue("cust-1", "api-calls", "2026-08");
        var otherLeaf = new PartitionKeyValue("cust-1", "api-calls", "2026-09");

        // Assert
        partitionKey.ShouldBe(same);
        (partitionKey == same).ShouldBeTrue();
        partitionKey.GetHashCode().ShouldBe(same.GetHashCode());

        partitionKey.ShouldNotBe(otherLeaf);
        (partitionKey != otherLeaf).ShouldBeTrue();
    }

    [Fact]
    public void Equals_ShouldNotFlattenTheComponents()
    {
        // Arrange & Act: joining the components into one string would make these two the same
        // partition, which is exactly the collision the store must not have
        var joined = new PartitionKeyValue("a/b");
        var hierarchical = new PartitionKeyValue("a", "b");

        // Assert
        joined.ShouldNotBe(hierarchical);
    }

    [Fact]
    public void Constructor_ShouldThrowForNull()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new PartitionKeyValue((string)null!));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyValueNull");
    }

    [Fact]
    public void Constructor_ShouldThrowForANullComponentOfAHierarchy()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new PartitionKeyValue("cust-1", null!));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyValueNull");
    }

    [Fact]
    public void Constructor_ShouldThrowWithoutAComponent()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new PartitionKeyValue(new List<string>()));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyValueEmpty");
    }

    [Fact]
    public void Constructor_ShouldThrowForMoreComponentsThanCosmosSupports()
    {
        // Arrange & Act
        var exception = Record.Exception(
            () => new PartitionKeyValue(new List<string> { "a", "b", "c", "d" }));

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyValueTooDeep");
    }

    [Fact]
    public void Constructor_ShouldAcceptAnEmptyStringComponent()
    {
        // Arrange & Act: an empty string is a valid (if unusual) partition key and must not throw
        var partitionKey = new PartitionKeyValue(string.Empty);

        // Assert
        partitionKey[0].ShouldBe(string.Empty);
    }

    [Fact]
    public void WithComponent_ShouldReplaceOneComponentAndLeaveTheOriginalAlone()
    {
        // Arrange
        var partitionKey = new PartitionKeyValue("cust-1", "api-calls", "2026-08");

        // Act
        var prefixed = partitionKey.WithComponent(0, "tenant__cust-1");

        // Assert
        prefixed.Components.ShouldBe(new[] { "tenant__cust-1", "api-calls", "2026-08" });
        partitionKey.Components.ShouldBe(new[] { "cust-1", "api-calls", "2026-08" });
    }
}
