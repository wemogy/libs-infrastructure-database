using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.InMemory.Setup;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Setup;

[Collection("Sequential")]
public class DependencyInjectionTests
{
    [Fact]
    public void AddRepository_ShouldWork()
    {
        // Arrange
        var serviceCollection = new ServiceCollection();
        serviceCollection
            .AddInMemoryDatabaseClient()
            .AddRepository<IUserRepository>();

        // Act
        var userRepository = serviceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

        // Assert
        userRepository.ShouldNotBeNull();
    }
}
