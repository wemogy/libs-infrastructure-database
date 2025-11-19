using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task ReplaceAsync_ShouldWorkForMultipleTenants()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user1Id = user1.Id;
        var user1TenantId = user1.TenantId;
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);

        var msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.First().ShouldBeEquivalentTo(user1);
        var appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().ShouldBeEquivalentTo(user2);

        var updatedUser = User.Faker.Generate();
        updatedUser.Id = user1Id;
        updatedUser.TenantId = user1TenantId;

        // Act
        var msFinalUser = await MicrosoftUserRepository.ReplaceAsync(updatedUser);

        // Assert
        msFinalUser.ShouldBeEquivalentTo(updatedUser);
        msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.Count.ShouldBe(1);
        msUsers.First().ShouldBeEquivalentTo(updatedUser);

        appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().ShouldBeEquivalentTo(user2); // should not update
    }
}
