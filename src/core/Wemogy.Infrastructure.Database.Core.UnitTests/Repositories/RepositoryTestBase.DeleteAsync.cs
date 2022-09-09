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
    public async Task DeleteAsyncShouldWork()
    {
        // Arrange
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act
        var userExistsBeforeDeletion = await UserRepository.ExistsAsync(user.Id, user.TenantId);
        await UserRepository.DeleteAsync(user.Id, user.TenantId);
        var userExistsAfterDeletion = await UserRepository.ExistsAsync(user.Id, user.TenantId);

        // Assert
        userExistsBeforeDeletion.Should().BeTrue();
        userExistsAfterDeletion.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsyncShouldThrowForNonExistingEntities()
    {
        // Arrange
        var notExistingUserId = Guid.NewGuid();
        var notExistingTenantId = Guid.NewGuid();

        // Act
        var exception = await Record.ExceptionAsync(() => UserRepository.DeleteAsync(notExistingUserId, notExistingTenantId));

        // Assert
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task DeleteAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        await UserRepository.CreateAsync(User.Faker.Generate());

        // Act
        await UserRepository.DeleteAsync(x => true);

        // Assert
        var entities = await UserRepository.QueryAsync(x => true);
        entities.Should().BeEmpty();
    }
}
