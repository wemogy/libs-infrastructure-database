using System;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act
        var result = await UserRepository.ExistsAsync(user.Id);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfItemNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act
        var result = await UserRepository.ExistsAsync(Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueIfIdOrPartitionKeyWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act
        var result = await UserRepository.ExistsAsync(user.Id, user.TenantId);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfIdOrPartitionKeyNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act
        var result = await UserRepository.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task EnsureExistAsync_ShouldWorkIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act & Assert
        await UserRepository.EnsureExistAsync(user.Id);
    }

    [Fact]
    public async Task EnsureExistAsync_ShouldThrowIfItemNotWasFound()
    {
        // Arrange
        await ResetAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundErrorException>(
            () => UserRepository.EnsureExistAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task EnsureExistAsync_ShouldWorkIfIdOrPartitionKeyFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act & Assert
        await UserRepository.EnsureExistAsync(user.Id, user.TenantId);
    }

    [Fact]
    public async Task EnsureExistAsync_ShouldThrowIfIdOrPartitionKeyNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundErrorException>(
            () => UserRepository.EnsureExistAsync(Guid.NewGuid(), Guid.NewGuid()));
    }
}
