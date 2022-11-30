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
    public async Task GetAsync_ShouldThrow_ForSoftDeletedItemsByIdAndPartitionKey()
    {
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted.Should().BeFalse();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        await MicrosoftUserRepository.DeleteAsync(
            user.Id,
            user.TenantId);

        // Assert
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.GetAsync(
                user.Id,
                user.TenantId));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task GetAsync_ShouldThrow_ForSoftDeletedItemsById()
    {
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted.Should().BeFalse();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        await MicrosoftUserRepository.DeleteAsync(user.Id);

        // Assert
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.GetAsync(user.Id));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task GetAsync_ShouldThrow_ForSoftDeletedItemsByPredicate()
    {
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted.Should().BeFalse();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        await MicrosoftUserRepository.DeleteAsync(
            i => i.Id == user.Id
                 && i.TenantId == user.TenantId);

        // Assert
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.GetAsync(user.Id));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnSoftDeletedItemWhenSoftDeleteIsDisabled()
    {
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted.Should().BeFalse();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        await MicrosoftUserRepository.DeleteAsync(
            user.Id,
            user.TenantId);
        MicrosoftUserRepository.SoftDeleteState.Disable();

        // Assert
        var userFromDb = await MicrosoftUserRepository.GetAsync(
            user.Id,
            user.TenantId);

        userFromDb.Should().BeEquivalentTo(
            user,
            options => options.Excluding(i => i.IsDeleted));
        userFromDb.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnSoftDeletedItemByPredicateWhenSoftDeleteIsDisabled()
    {
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted.Should().BeFalse();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        await MicrosoftUserRepository.DeleteAsync(
            i => i.Id == user.Id
                 && i.TenantId == user.TenantId);
        MicrosoftUserRepository.SoftDeleteState.Disable();

        // Assert
        var userFromDb = await MicrosoftUserRepository.GetAsync(
            i => i.Id == user.Id
                 && i.TenantId == user.TenantId);

        userFromDb.Should().BeEquivalentTo(
            user,
            options => options.Excluding(i => i.IsDeleted));
        userFromDb.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task GetAsync_ShouldReturnSoftDeletedItemByIdWhenSoftDeleteIsDisabled()
    {
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted.Should().BeFalse();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        await MicrosoftUserRepository.DeleteAsync(user.Id);
        MicrosoftUserRepository.SoftDeleteState.Disable();

        // Assert
        var userFromDb = await MicrosoftUserRepository.GetAsync(user.Id);

        userFromDb.Should().BeEquivalentTo(
            user,
            options => options.Excluding(i => i.IsDeleted));
        userFromDb.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDeleteByIdAndPartitionKeyShouldThrowIfNotExisting()
    {
        // Arrange
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString()));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task SoftDeleteByIdShouldThrowIfNotExisting()
    {
        // Arrange
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync(
                Guid.NewGuid().ToString()));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task SoftDeleteByPredicateShouldNotThrowIfNotExisting()
    {
        // Arrange
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync(
                i => i.Id == Guid.NewGuid().ToString()
                     && i.TenantId == Guid.NewGuid().ToString()));

        // Assert
        exception.Should().BeNull();
    }
}
