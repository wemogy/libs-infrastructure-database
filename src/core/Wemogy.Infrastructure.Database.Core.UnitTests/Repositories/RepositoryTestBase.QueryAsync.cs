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
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var queriedUser = await MicrosoftUserRepository.QueryAsync(queryParameters);

        // Assert
        queriedUser.Should().HaveCount(1);
        queriedUser[0].TenantId.Should().Be(user.TenantId);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnAllNotDeletedItemsIfEmptyQueryParameters()
    {
        // Arrange
        await ResetAsync();
        var queryParameters = new QueryParameters();
        var user = User.Faker.Generate();
        user.IsDeleted = true;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var queriedUser = await MicrosoftUserRepository.QueryAsync(queryParameters);

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
            await MicrosoftUserRepository.CreateAsync(user);
        }

        // Act
        var queriedUser = await MicrosoftUserRepository.QueryAsync(queryParameters);

        // Assert
        queriedUser.Should().HaveCount(queryParameters.Take.Value);
    }

    [Fact]
    public async Task QueryAsync_LambdaShouldRespectTakeCount()
    {
        // Arrange
        var paginationParameters = new PaginationParameters(
            0,
            5);
        await ResetAsync();
        var users = User.Faker.Generate(10);
        foreach (var user in users)
        {
            await MicrosoftUserRepository.CreateAsync(user);
        }

        // Act
        var queriedUser = await MicrosoftUserRepository.QueryAsync(
            x => true,
            paginationParameters);

        // Assert
        queriedUser.Should().HaveCount(paginationParameters.Take);
    }

    [Fact]
    public async Task QueryAsync_LambdaShouldRespectSkipCount()
    {
        // Arrange
        var paginationParameters = new PaginationParameters(
            2,
            10);
        await ResetAsync();
        var users = User.Faker.Generate(10);
        foreach (var user in users)
        {
            await MicrosoftUserRepository.CreateAsync(user);
        }

        // Act
        var queriedUser = await MicrosoftUserRepository.QueryAsync(
            x => true,
            paginationParameters);

        // Assert
        queriedUser.Should().HaveCount(users.Count - paginationParameters.Skip);
    }
}
