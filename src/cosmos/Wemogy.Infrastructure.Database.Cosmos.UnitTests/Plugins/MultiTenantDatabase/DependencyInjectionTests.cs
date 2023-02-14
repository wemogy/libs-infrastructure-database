using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Abstractions;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public class DependencyInjectionTests : CosmosUnitTestBase
{
    [Fact]
    public void AddRepository_ShouldWork()
    {
        // Arrange
        var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
            ConnectionString,
            DatabaseName,
            new List<string> { "animals", "files", "users" });
        ServiceCollection
            .AddDatabase(cosmosDatabaseClientFactory)
            .AddRepository<IUserRepository>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        Assert.NotNull(userRepository);
    }
}
