using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Abstractions;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Setup;

public class DependencyInjectionTests : CosmosUnitTestBase
{
    [Fact]
    public void AddRepository_ShouldWork()
    {
        // Arrange
        var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
            ConnectionString,
            DatabaseName);
        ServiceCollection
            .AddDatabase(cosmosDatabaseClientFactory)
            .AddRepository<IUserRepository>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        userRepository.ShouldNotBeNull();
    }

    [Fact]
    public void AddMultiTenantDatabaseRepository_ShouldWork()
    {
        // Arrange
        var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
            ConnectionString,
            DatabaseName);
        ServiceCollection.AddSingleton<AppleTenantProvider>();
        ServiceCollection
            .AddDatabase(cosmosDatabaseClientFactory)
            .AddRepository<IUserRepository, AppleTenantProvider>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        userRepository.ShouldNotBeNull();
    }

    [Fact]
    public void AddRepository_InitializesContainers_ShouldWork()
    {
        // Arrange
        var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
            ConnectionString,
            DatabaseName,
            true,
            false,
            new List<string> { "users" });
        ServiceCollection
            .AddDatabase(cosmosDatabaseClientFactory)
            .AddRepository<IUserRepository>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        userRepository.ShouldNotBeNull();
    }

    [Fact]
    public void AddMultiTenantDatabaseRepository_InitializesContainers_ShouldWork()
    {
        // Arrange
        var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
            ConnectionString,
            DatabaseName,
            true,
            false,
            new List<string> { "users" });
        ServiceCollection.AddSingleton<AppleTenantProvider>();
        ServiceCollection
            .AddDatabase(cosmosDatabaseClientFactory)
            .AddRepository<IUserRepository, AppleTenantProvider>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        userRepository.ShouldNotBeNull();
    }
}
