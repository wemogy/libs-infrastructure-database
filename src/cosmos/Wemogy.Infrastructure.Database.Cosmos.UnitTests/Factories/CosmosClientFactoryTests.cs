using System.Collections.Generic;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Abstractions;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Factories;

public class CosmosClientFactoryTests : CosmosUnitTestBase
{
    [Fact]
    public void FromConnectionString_ShouldWorkForEmulator()
    {
        // Arrange

        // Act
        var cosmosClient = AzureCosmosClientFactory.FromConnectionString(
            ConnectionString,
            true);

        // Assert
        Assert.NotNull(cosmosClient);
    }

    [Fact]
    public void FromConnectionString_InitializesContainers_ShouldWorkForEmulator()
    {
        // Arrange

        // Act
        var cosmosClient = AzureCosmosClientFactory.FromConnectionString(
            ConnectionString,
            true,
            new List<(string, string)> { ("test", "test") },
            "test");

        // Assert
        Assert.NotNull(cosmosClient);
    }
}
