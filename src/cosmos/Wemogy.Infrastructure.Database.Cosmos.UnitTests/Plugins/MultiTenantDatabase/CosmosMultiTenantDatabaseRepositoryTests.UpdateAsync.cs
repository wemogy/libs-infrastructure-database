using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public partial class CosmosMultiTenantDatabaseRepositoryTests
{
    [Fact]
    public async Task UpdateAsync_ShouldWorkForMultipleTenants()
    {
        // Arrange
        await ResetAsync();

        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        await AppleUserRepository.CreateAsync(user);

        // Act
        var updatedMsUser = await MicrosoftUserRepository.UpdateAsync(
            user.Id,
            user.TenantId,
            UpdateAction);

        // Assert
        updatedMsUser.Firstname.Should().Be("Updated");
        updatedMsUser.TenantId.Should().Be(user.TenantId);

        // apple user should remain intact!
        var appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task UpdateAsyncWithoutPartitionKey_ShouldWorkForMultipleTenants()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        await AppleUserRepository.CreateAsync(user);

        // Act
        var updatedUser = await MicrosoftUserRepository.UpdateAsync(
            user.Id,
            UpdateAction);

        // Assert
        updatedUser.Firstname.Should().Be("Updated");
        updatedUser.TenantId.Should().Be(user.TenantId);

        // apple user should remain intact!
        var appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().Should().BeEquivalentTo(user);
    }

    private void UpdateAction(User u)
    {
        u.Firstname = "Updated";
    }
}
