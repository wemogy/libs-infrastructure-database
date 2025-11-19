using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTestsBase
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
        updatedMsUser.Firstname.ShouldBe("Updated");
        updatedMsUser.TenantId.ShouldBe(user.TenantId);

        // apple user should remain intact!
        var appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().ShouldBeEquivalentTo(user);
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
        updatedUser.Firstname.ShouldBe("Updated");
        updatedUser.TenantId.ShouldBe(user.TenantId);

        // apple user should remain intact!
        var appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().ShouldBeEquivalentTo(user);
    }

    private void UpdateAction(User u)
    {
        u.Firstname = "Updated";
    }
}
