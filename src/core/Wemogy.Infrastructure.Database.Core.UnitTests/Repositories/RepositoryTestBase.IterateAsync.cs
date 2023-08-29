using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task IterateAsync_ShouldGetAnExistingItemByIdWithExpression()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        var count = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(x => x.Id == user.Id, _ =>
        {
            count++;
        });

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task IterateAsync_ShouldGetAnExistingItemByPartitionKeyWithExpression()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        var count = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(x => x.TenantId == user.TenantId, _ =>
        {
            count++;
        });

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task IterateAsync_ShouldWorkWithAsynchronousCallback()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        var count = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(x => x.TenantId == user.TenantId, _ =>
        {
            count++;

            return Task.CompletedTask;
        });

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task IterateAsync_ShouldSupportPagination()
    {
        // Arrange
        var take = 5;
        var tenantId = Guid.NewGuid().ToString();
        await ResetAsync();
        for (int i = 0; i < 20; i++)
        {
            var user = User.Faker.Generate();
            user.TenantId = tenantId;
            await MicrosoftUserRepository.CreateAsync(user);
        }

        var firstDocumentsCount = 0;
        var lastDocumentsCount = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(
            x => x.TenantId == tenantId,
            new Pagination(0, take),
            _ =>
        {
            firstDocumentsCount++;
        });

        await MicrosoftUserRepository.IterateAsync(
            x => x.TenantId == tenantId,
            new Pagination(18, take),
            _ =>
            {
                lastDocumentsCount++;
            });

        // Assert
        firstDocumentsCount.Should().Be(take);
        lastDocumentsCount.Should().Be(2);
    }

    [Theory]
    [InlineData(SortDirection.Ascending)]
    [InlineData(SortDirection.Descending)]
    public async Task IterateAsync_ShouldSupportSorting(SortDirection sortDirection)
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
        var callbackUsers = new List<User>();

        // Act
        await MicrosoftUserRepository.IterateAsync(
            x => x.TenantId == tenantId,
            sortingParameters,
            callbackUsers.Add);

        // Assert that callbackUsers are sorted by Firstname
        for (int i = 0; i < callbackUsers.Count - 1; i++)
        {
            var current = callbackUsers[i];
            var next = callbackUsers[i + 1];
            var sortOrder = string.Compare(
                current.Firstname,
                next.Firstname,
                StringComparison.Ordinal);
            if (sortDirection == SortDirection.Ascending)
            {
                sortOrder.Should().BeLessOrEqualTo(0);
            }
            else
            {
                sortOrder.Should().BeGreaterOrEqualTo(0);
            }
        }
    }
}
