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
        var user1Raw = User.Faker.Generate();
        var user1Id = user1Raw.Id;
        var user1TenantId = user1Raw.TenantId;
        var user1 = await MicrosoftUserRepository.CreateAsync(user1Raw);
        var user2 = await AppleUserRepository.CreateAsync(User.Faker.Generate());

        var msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.First().ShouldBeEquivalentTo(user1);
        var appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().ShouldBeEquivalentTo(user2);

        var updatedUser = User.Faker
            .RuleFor(x => x.Id, user1Id)
            .Generate();
        updatedUser.TenantId = user1TenantId;

        // Act
        var msFinalUser = await MicrosoftUserRepository.ReplaceAsync(updatedUser);

        // Assert: the returned entity matches the DB state after replace
        var readBack = await MicrosoftUserRepository.GetAsync(msFinalUser.Id, msFinalUser.TenantId);
        msFinalUser.ShouldBeEquivalentTo(readBack);
        msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.Count.ShouldBe(1);
        msUsers.First().ShouldBeEquivalentTo(msFinalUser);

        appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().ShouldBeEquivalentTo(user2); // should not update
    }
}
