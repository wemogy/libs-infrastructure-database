using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Abstractions;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Factories;

public class CosmosDatabaseRepositoryFactoryTests : CosmosUnitTestBase
{
    [Fact]
    public void CreateDatabaseRepositoryWithOptionsAttribute_ShouldWork()
    {
        // Arrange
        var factory = new CosmosDatabaseRepositoryFactory(ServiceCollection, ConnectionString);

        // Act
        var repository = factory.CreateDatabaseRepository<IUserRepository>(DatabaseName);

        // Assert
        Assert.NotNull(repository);
    }

    [Fact]
    public void CreateDatabaseRepositoryWithoutAttribute_ShouldWork()
    {
        // Arrange
        var factory = new CosmosDatabaseRepositoryFactory(ServiceCollection, ConnectionString);

        // Act
        var repository = factory.CreateDatabaseRepository<IFileRepository>(DatabaseName);

        // Assert
        Assert.NotNull(repository);
    }
}
