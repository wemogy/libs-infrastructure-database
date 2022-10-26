using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task GetAsync_ShouldGetAnExistingItemById()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act
        var userFromDb = await UserRepository.GetAsync(user.Id);

        // Assert
        userFromDb.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task GetAsync_ShouldThrowIfItemNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundErrorException>(
            () => UserRepository.GetAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task GetAsync_ShouldGetAnExistingItemByIdAndPartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act
        var userFromDb = await UserRepository.GetAsync(
            user.Id,
            user.TenantId);

        // Assert
        userFromDb.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task GetAsync_ShouldThrowIfIdOrPartitionKeyNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundErrorException>(
            () => UserRepository.GetAsync(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()));
    }
}
