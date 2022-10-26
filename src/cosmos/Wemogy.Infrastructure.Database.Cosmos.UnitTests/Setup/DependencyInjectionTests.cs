using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
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
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger>();
        var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
            ConnectionString,
            DatabaseName,
            logger);
        ServiceCollection
            .AddDatabase(cosmosDatabaseClientFactory)
            .AddRepository<IUserRepository>();

        // Act
        var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        Assert.NotNull(userRepository);
    }
}
