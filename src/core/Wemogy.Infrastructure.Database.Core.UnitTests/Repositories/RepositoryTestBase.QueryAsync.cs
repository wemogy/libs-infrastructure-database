using System;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;
using SortDirection = Wemogy.Infrastructure.Database.Core.Enums.SortDirection;

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
        queriedUser.ShouldHaveSingleItem();
        queriedUser[0].TenantId.ShouldBe(user.TenantId);
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
        queriedUser.Count.ShouldBe(0);
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
        queriedUser.Count.ShouldBe(queryParameters.Take.Value);
    }

    [Fact]
    public async Task QueryAsync_LambdaShouldRespectTakeCount()
    {
        // Arrange
        var pagination = new Pagination(
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
            pagination);

        // Assert
        queriedUser.Count.ShouldBe(pagination.Take);
    }

    [Fact]
    public async Task QueryAsync_LambdaShouldRespectSkipCount()
    {
        // Arrange
        var pagination = new Pagination(
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
            pagination);

        // Assert
        queriedUser.Count.ShouldBe(users.Count - pagination.Skip);
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task QueryAsync_LambdaShouldRespectSorting(SortDirection sortDirection)
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        await ResetAsync();
        for (int i = 0; i < 20; i++)
        {
            var user = User.Faker.Generate();
            user.TenantId = tenantId;
            await MicrosoftUserRepository.CreateAsync(user);
        }

        var sortingParameters = new Sorting<User>()
            .OrderBy(x => x.Firstname, sortDirection);

        // Act
        var queriedUser = await MicrosoftUserRepository.QueryAsync(
            x => true,
            sortingParameters);

        // Assert that queriedUser are sorted by Firstname
        for (int i = 0; i < queriedUser.Count - 1; i++)
        {
            var current = queriedUser[i];
            var next = queriedUser[i + 1];
            var sortOrder = string.Compare(
                current.Firstname,
                next.Firstname,
                StringComparison.Ordinal);
            if (sortDirection == SortDirection.Ascending)
            {
                sortOrder.ShouldBeLessThanOrEqualTo(0);
            }
            else
            {
                sortOrder.ShouldBeGreaterThanOrEqualTo(0);
            }
        }
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task QueryAsync_LambdaShouldRespectPaginationAndSorting(SortDirection sortDirection)
    {
        // Arrange
        var tenantId = Guid.NewGuid().ToString();
        await ResetAsync();
        for (int i = 0; i < 20; i++)
        {
            var user = User.Faker.Generate();
            user.TenantId = tenantId;
            await MicrosoftUserRepository.CreateAsync(user);
        }

        var sortingParameters = new Sorting<User>()
            .OrderBy(x => x.Firstname, sortDirection);
        var pagination = new Pagination(
            2,
            10);

        // Act
        var queriedUser = await MicrosoftUserRepository.QueryAsync(
            x => true,
            sortingParameters,
            pagination);

        // Assert that queriedUser are sorted by Firstname
        for (int i = 0; i < queriedUser.Count - 1; i++)
        {
            var current = queriedUser[i];
            var next = queriedUser[i + 1];
            var sortOrder = string.Compare(
                current.Firstname,
                next.Firstname,
                StringComparison.Ordinal);
            if (sortDirection == SortDirection.Ascending)
            {
                sortOrder.ShouldBeLessThanOrEqualTo(0);
            }
            else
            {
                sortOrder.ShouldBeGreaterThanOrEqualTo(0);
            }
        }

        // Assert that queriedUser are paginated
        queriedUser.Count.ShouldBe(pagination.Take);
    }
}
