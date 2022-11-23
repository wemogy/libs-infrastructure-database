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
    public async Task EnsureExistsAsync_ShouldWorkIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var exception = await Record.ExceptionAsync(() => MicrosoftUserRepository.EnsureExistsAsync(user.Id));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldThrowIfItemNotWasFound()
    {
        // Arrange
        await ResetAsync();

        // Act
        var exception = await Record.ExceptionAsync(() => MicrosoftUserRepository.EnsureExistsAsync(Guid.NewGuid().ToString()));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldWorkIfIdAndPartitionKeyFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.EnsureExistsAsync(
                user.Id,
                user.TenantId));

        // Assert
        exception.Should().BeNull();
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldThrowIfIdAndPartitionKeyNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.EnsureExistsAsync(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task EnsureExistsAsync_ShouldThrowIfItemWasNotFoundInPartition()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.EnsureExistsAsync(
                user.Id,
                Guid.NewGuid().ToString()));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }
}
