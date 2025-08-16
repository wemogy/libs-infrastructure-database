using System.Collections.Generic;
using Shouldly;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Abstractions;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
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
            true,
            applicationName: "test-app-name");

        // Assert
        cosmosClient.ShouldNotBeNull();
    }

    [Fact]
    public void FromConnectionString_InitializesContainers_ShouldWorkForEmulator()
    {
        // Arrange

        // Act
        var cosmosClient = AzureCosmosClientFactory.FromConnectionString(
            ConnectionString,
            true,
            new List<(string, string)> { (TestingConstants.DatabaseName, "users") },
            "test-app-name");

        // Assert
        cosmosClient.ShouldNotBeNull();
    }
}
