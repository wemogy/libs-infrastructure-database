using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task QueryAsync_ShouldReturnAllItemsIfEmptyQueryParameters()
    {
        // Arrange
        await ResetAsync();
        var queryParameters = new QueryParameters();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act
        var queriedUser = await UserRepository.QueryAsync(queryParameters);

        // Assert
        queriedUser.Should().HaveCount(1);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnAllNotDeletedItemsIfEmptyQueryParameters()
    {
        // Arrange
        await ResetAsync();
        var queryParameters = new QueryParameters();
        var user = User.Faker.Generate();
        user.IsDeleted = true;
        await UserRepository.CreateAsync(user);

        // Act
        var queriedUser = await UserRepository.QueryAsync(queryParameters);

        // Assert
        queriedUser.Should().HaveCount(0);
    }

    [Fact]
    public async Task QueryAsync_ShouldRespectTakeCount()
    {
        // Arrange
        await ResetAsync();
        var queryParameters = new QueryParameters
        {
            Take = 5
        };
        var users = User.Faker.Generate(10);
        foreach (var user in users)
        {
            await UserRepository.CreateAsync(user);
        }

        // Act
        var queriedUser = await UserRepository.QueryAsync(queryParameters);

        // Assert
        queriedUser.Should().HaveCount(queryParameters.Take.Value);
    }
}
