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
    public async Task DeleteAsyncShouldWork()
    {
        // Arrange
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
        userExistsBeforeDeletion.ShouldBeTrue();
        userExistsAfterDeletion.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsyncWithIdOnlyShouldWork()
    {
        // Arrange
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userExistsBeforeDeletion = await MicrosoftUserRepository.ExistsAsync(
            user.Id);
        await MicrosoftUserRepository.DeleteAsync(
            user.Id);
        var userExistsAfterDeletion = await MicrosoftUserRepository.ExistsAsync(
            user.Id);

        // Assert
        userExistsBeforeDeletion.ShouldBeTrue();
        userExistsAfterDeletion.ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsyncShouldThrowForNonExistingEntities()
    {
        // Arrange
        var notExistingUserId = Guid.NewGuid().ToString();
        var notExistingTenantId = Guid.NewGuid().ToString();

        // Act & Assert
        await Should.ThrowAsync<NotFoundErrorException>(() => MicrosoftUserRepository.DeleteAsync(
            notExistingUserId,
            notExistingTenantId));
    }

    [Fact]
    public async Task DeleteAsync_ShouldNameThePartitionKeyWhenTheEntityIsMissing()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        var exception = await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.DeleteAsync(
                user.Id,
                user.TenantId));

        // Assert: the partition the caller asked for, so the message says where it looked
        exception.Description.ShouldContain(user.Id);
        exception.Description.ShouldContain(user.TenantId);
    }

    [Fact]
    public async Task DeleteAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());

        // Act
        await MicrosoftUserRepository.DeleteAsync(x => true);

        // Assert
        var entities = await MicrosoftUserRepository.QueryAsync(x => true);
        entities.ShouldBeEmpty();
    }
}
