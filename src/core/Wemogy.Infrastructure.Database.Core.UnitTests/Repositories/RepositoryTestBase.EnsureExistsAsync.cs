using System;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task EnsureExistsAsync_ShouldWorkIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act & Assert
        await Should.NotThrowAsync(() => MicrosoftUserRepository.EnsureExistsAsync(user.Id));
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldThrowIfItemNotWasFound()
    {
        // Arrange
        await ResetAsync();

        // Act & Assert
        await Should.ThrowAsync<NotFoundErrorException>(() =>
            MicrosoftUserRepository.EnsureExistsAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldWorkIfIdAndPartitionKeyFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act & Assert
        await Should.NotThrowAsync(
            () => MicrosoftUserRepository.EnsureExistsAsync(
                user.Id,
                user.TenantId));
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldThrowIfIdAndPartitionKeyNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act & Assert
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.EnsureExistsAsync(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldThrowIfItemWasNotFoundInPartition()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act & Assert
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.EnsureExistsAsync(
                user.Id,
                Guid.NewGuid().ToString()));
    }
}
