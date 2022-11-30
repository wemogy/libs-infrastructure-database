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
    public async Task DeleteAsyncByIdAndPartitionKeyShouldWork()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userExistsBeforeDeletion = await MicrosoftUserRepository.ExistsAsync(
            user.Id,
            user.TenantId);
        await MicrosoftUserRepository.DeleteAsync(
            user.Id,
            user.TenantId);
        var userExistsAfterDeletion = await MicrosoftUserRepository.ExistsAsync(
            user.Id,
            user.TenantId);

        // Assert
        userExistsBeforeDeletion.Should().BeTrue();
        userExistsAfterDeletion.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsyncByPredicateShouldWork()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userExistsBeforeDeletion = await MicrosoftUserRepository.ExistsAsync(
            i => i.Id == user.Id && i.TenantId == user.TenantId);
        var count = await MicrosoftUserRepository.DeleteAsync(
            i => i.Id == user.Id && i.TenantId == user.TenantId);
        var userExistsAfterDeletion = await MicrosoftUserRepository.ExistsAsync(
            i => i.Id == user.Id && i.TenantId == user.TenantId);

        // Assert
        userExistsBeforeDeletion.Should().BeTrue();
        userExistsAfterDeletion.Should().BeFalse();
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteAsyncByIdShouldWork()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userExistsBeforeDeletion = await MicrosoftUserRepository.ExistsAsync(user.Id);
        await MicrosoftUserRepository.DeleteAsync(user.Id);
        var userExistsAfterDeletion = await MicrosoftUserRepository.ExistsAsync(user.Id);

        // Assert
        userExistsBeforeDeletion.Should().BeTrue();
        userExistsAfterDeletion.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsyncByIdAndPartitionKeyShouldThrowForNonExistingEntities()
    {
        // Arrange
        var notExistingUserId = Guid.NewGuid().ToString();
        var notExistingTenantId = Guid.NewGuid().ToString();

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync(
                notExistingUserId,
                notExistingTenantId));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task DeleteAsyncByPredicateShouldNotThrowForNonExistingEntities()
    {
        // Arrange
        var notExistingUserId = Guid.NewGuid().ToString();
        var notExistingTenantId = Guid.NewGuid().ToString();

        // Act
        var count = await MicrosoftUserRepository.DeleteAsync(
            i => i.Id == notExistingUserId && i.TenantId == notExistingTenantId);

        // Assert
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsyncByIdShouldThrowForNonExistingEntities()
    {
        // Arrange
        var notExistingUserId = Guid.NewGuid().ToString();

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync(notExistingUserId));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task DeleteAllAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());

        // Act
        var count = await MicrosoftUserRepository.DeleteAsync(x => true);

        // Assert
        var entities = await MicrosoftUserRepository.QueryAsync(x => true);
        entities.Should().BeEmpty();
        count.Should().Be(4);
    }
}
