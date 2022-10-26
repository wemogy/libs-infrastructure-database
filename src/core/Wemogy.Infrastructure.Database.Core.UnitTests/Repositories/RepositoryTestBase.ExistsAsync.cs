using System;
using System.Threading.Tasks;
using FluentAssertions;
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
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfItemNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act
        var result = await UserRepository.ExistsAsync(Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueIfIdAndPartitionKeyWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act
        var result = await UserRepository.ExistsAsync(user.Id, user.TenantId);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfIdAndPartitionKeyNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act
        var result = await UserRepository.ExistsAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldThrowIfItemWasNotFoundInPartition()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act
        var result = await UserRepository.ExistsAsync(user.Id, Guid.NewGuid());

        // Assert
        result.Should().BeFalse();
    }
}
