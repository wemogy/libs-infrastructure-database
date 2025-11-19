using System;
using System.Threading.Tasks;
using Shouldly;
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
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var result = await MicrosoftUserRepository.ExistsAsync(user.Id);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfItemNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act
        var result = await MicrosoftUserRepository.ExistsAsync(Guid.NewGuid().ToString());

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnTrueIfIdAndPartitionKeyWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var result = await MicrosoftUserRepository.ExistsAsync(
            user.Id,
            user.TenantId);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_ShouldReturnFalseIfIdAndPartitionKeyNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act
        var result = await MicrosoftUserRepository.ExistsAsync(
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_ShouldThrowIfItemWasNotFoundInPartition()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var result = await MicrosoftUserRepository.ExistsAsync(
            user.Id,
            Guid.NewGuid().ToString());

        // Assert
        result.ShouldBeFalse();
    }
}
