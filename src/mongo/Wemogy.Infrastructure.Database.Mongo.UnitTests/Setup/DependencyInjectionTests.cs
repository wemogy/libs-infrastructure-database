using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Mongo.Factories;
using Wemogy.Infrastructure.Database.Mongo.UnitTests.Abstractions;
using Xunit;

namespace Wemogy.Infrastructure.Database.Mongo.UnitTests.Setup;

public class DependencyInjectionTests : MongoUnitTestBase
{
    [Fact]
    public void AddRepository_ShouldWork()
    {
        // Arrange
        var mongoDatabaseClientFactory = new MongoDatabaseClientFactory(
            ConnectionString,
            DatabaseName);
        ServiceCollection
            .AddDatabase(mongoDatabaseClientFactory)
            .AddRepository<IUserRepository>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        Assert.NotNull(userRepository);
    }

    [Fact]
    public void AddMultiTenantDatabaseRepository_ShouldWork()
    {
        // Arrange
        var mongoDatabaseClientFactory = new MongoDatabaseClientFactory(
            ConnectionString,
            DatabaseName);
        ServiceCollection.AddSingleton<AppleTenantProvider>();
        ServiceCollection
            .AddDatabase(mongoDatabaseClientFactory)
            .AddRepository<IUserRepository, AppleTenantProvider>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        Assert.NotNull(userRepository);
    }

    [Fact]
    public void AddRepository_InitializesContainers_ShouldWork()
    {
        // Arrange
        var mongoDatabaseClientFactory = new MongoDatabaseClientFactory(
            ConnectionString,
            DatabaseName);
        ServiceCollection
            .AddDatabase(mongoDatabaseClientFactory)
            .AddRepository<IUserRepository>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        Assert.NotNull(userRepository);
    }

    [Fact]
    public void AddMultiTenantDatabaseRepository_InitializesContainers_ShouldWork()
    {
        // Arrange
        var mongoDatabaseClientFactory = new MongoDatabaseClientFactory(
            ConnectionString,
            DatabaseName,
            true);
        ServiceCollection.AddSingleton<AppleTenantProvider>();
        ServiceCollection
            .AddDatabase(mongoDatabaseClientFactory)
            .AddRepository<IUserRepository, AppleTenantProvider>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        Assert.NotNull(userRepository);
    }
}
