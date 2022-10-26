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
    public async Task UpdateAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        void UpdateAction(User u)
        {
            u.Firstname = "Updated";
        }

        // Act
        var updatedUser = await UserRepository.UpdateAsync(
            user.Id,
            user.TenantId,
            UpdateAction);

        // Assert
        updatedUser.Firstname.Should().Be("Updated");
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
        await Assert.ThrowsAsync<NotFoundErrorException>(
            () => UserRepository.UpdateAsync(
                Guid.NewGuid().ToString(),
                Guid.NewGuid().ToString(),
                UpdateAction));
    }
}
