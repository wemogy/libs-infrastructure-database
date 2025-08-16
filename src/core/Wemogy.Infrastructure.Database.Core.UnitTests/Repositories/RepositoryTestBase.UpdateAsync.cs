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
    public async Task UpdateAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        void UpdateAction(User u)
        {
            u.Firstname = "Updated";
        }

        // Act
        var updatedUser = await MicrosoftUserRepository.UpdateAsync(
            user.Id,
            user.TenantId,
            UpdateAction);

        // Assert
        updatedUser.Firstname.ShouldBe("Updated");
        updatedUser.TenantId.ShouldBe(user.TenantId);
    }

    [Fact]
    public async Task UpdateAsyncWithoutPartitionKey_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        void UpdateAction(User u)
        {
            u.Firstname = "Updated";
        }

        // Act
        var updatedUser = await MicrosoftUserRepository.UpdateAsync(
            user.Id,
            UpdateAction);

        // Assert
        updatedUser.Firstname.ShouldBe("Updated");
        updatedUser.TenantId.ShouldBe(user.TenantId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowIfTheItemNotExists()
    {
        // Arrange
        await ResetAsync();

        void UpdateAction(User user)
        {
            user.Firstname = "Updated";
        }

        // Act & Assert
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.UpdateAsync(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                UpdateAction));
    }
}
