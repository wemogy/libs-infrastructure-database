using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task GetAsync_ShouldThrow_ForSoftDeletedItemsForMultipleTenants()
    {
        await ResetAsync();
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted.Should().BeFalse();
        await MicrosoftUserRepository.CreateAsync(user);
        await AppleUserRepository.CreateAsync(user);

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
        AssertExceptionMessageDoesNotContainPrefix(exception);

        var appleUser = await AppleUserRepository.GetAsync(
            user.Id,
            user.TenantId);
        appleUser.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnSoftDeletedItemWhenSoftDeleteIsDisabledForMultipleTenants()
    {
        await ResetAsync();
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted.Should().BeFalse();
        await MicrosoftUserRepository.CreateAsync(user);
        await AppleUserRepository.CreateAsync(user);

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
    public async Task SoftDeleteByIdAndPartitionKeyShouldThrowIfNotExistingInMultiTenant()
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
        AssertExceptionMessageDoesNotContainPrefix(exception);
    }

    [Fact]
    public async Task SoftDeleteByIdShouldThrowIfNotExistingInMultiTenant()
    {
        // Arrange
        MicrosoftUserRepository.SoftDeleteState.Enable();

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync(
                Guid.NewGuid().ToString()));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
        AssertExceptionMessageDoesNotContainPrefix(exception);
    }
}
