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
            "test",
            new List<(string, string)> { ("test", "test") },
            true);

        // Assert
        Assert.NotNull(cosmosClient);
    }
}
