using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Client;
using Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Client;

/// <summary>
///     How an entity declares its partition key is resolved by the shared client base, so these
///     tests pin the rules for every provider even though they run against the in-memory one.
/// </summary>
[Collection("Sequential")]
public class InMemoryDatabaseClientPartitionKeyDefinitionTests
{
    [Fact]
    public async Task Client_ShouldAddressADocumentByItsWholeHierarchy()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<HierarchicalKeyedEntity>();
        await client.DeleteAsync(_ => true);

        var entity = new HierarchicalKeyedEntity
        {
            Id = "e1",
            CustomerId = "cust-1",
            MeterSlug = "api-calls",
            TimeBucket = "2026-08",
            Amount = 3
        };

        // Act: the key is read off the entity by the attributes, in the declared order
        await client.CreateAsync(entity);
        var fetchedEntity = await client.GetAsync(
            "e1",
            new PartitionKeyValue("cust-1", "api-calls", "2026-08"),
            CancellationToken.None);

        // Assert
        fetchedEntity.Amount.ShouldBe(3);
    }

    [Fact]
    public void Client_ShouldRejectAnEntityDeclaringBothAttributes()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new InMemoryDatabaseClient<AmbiguouslyKeyedEntity>());

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyDefinitionAmbiguous");
    }

    [Fact]
    public void Client_ShouldRejectAnEntityCarryingTheSingleAttributeTwice()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new InMemoryDatabaseClient<DoublySingleKeyedEntity>());

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyDefinitionAmbiguous");
    }

    [Fact]
    public void Client_ShouldRejectAGapInTheOrders()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new InMemoryDatabaseClient<GappedHierarchicalKeyedEntity>());

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyDefinitionAmbiguous");
    }

    [Fact]
    public void Client_ShouldRejectADuplicateOrder()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new InMemoryDatabaseClient<DuplicateOrderHierarchicalKeyedEntity>());

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyDefinitionAmbiguous");
    }

    [Fact]
    public void Client_ShouldRejectMoreComponentsThanCosmosSupports()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new InMemoryDatabaseClient<TooDeepHierarchicalKeyedEntity>());

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyValueTooDeep");
    }

    [Fact]
    public void Client_ShouldRejectAPartitionKeyPropertyThatIsNotAString()
    {
        // Arrange & Act: this used to surface as an InvalidCastException on the first write
        var exception = Record.Exception(() => new InMemoryDatabaseClient<NonStringKeyedEntity>());

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyPropertyNotAString");
    }

    [Fact]
    public void Client_ShouldRejectAnEntityWithoutAPartitionKey()
    {
        // Arrange & Act
        var exception = Record.Exception(() => new InMemoryDatabaseClient<UnkeyedEntity>());

        // Assert
        exception.ShouldBeOfType<UnexpectedErrorException>();
        ((UnexpectedErrorException)exception).Code.ShouldBe("PartitionKeyPropertyNotFound");
    }
}
