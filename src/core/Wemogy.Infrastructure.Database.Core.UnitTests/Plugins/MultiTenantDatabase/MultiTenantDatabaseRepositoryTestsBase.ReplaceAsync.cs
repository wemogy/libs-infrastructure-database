using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;
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
        msUsers.First().ShouldBeEquivalentToIgnoringETag(user1);
        var appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().ShouldBeEquivalentToIgnoringETag(user2);

        var updatedUser = User.Faker
            .RuleFor(x => x.Id, user1Id)
            .Generate();
        updatedUser.TenantId = user1TenantId;

        // Act
        var msFinalUser = await MicrosoftUserRepository.ReplaceAsync(updatedUser);

        // Assert
        msFinalUser.ShouldBeEquivalentToIgnoringETag(updatedUser);
        msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.Count.ShouldBe(1);
        msUsers.First().ShouldBeEquivalentToIgnoringETag(updatedUser);

        appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().ShouldBeEquivalentToIgnoringETag(user2); // should not update
    }
}
