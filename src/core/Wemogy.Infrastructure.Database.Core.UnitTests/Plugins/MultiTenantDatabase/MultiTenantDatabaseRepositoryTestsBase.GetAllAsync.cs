using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task GetAllAsyncMultiple_ShouldOnlyGetFromCorrectPartition()
    {
        // Arrange
        await ResetAsync();
        var appleUser1 = User.Faker.Generate();
        var appleUser2 = User.Faker.Generate();
        var appleUser3 = User.Faker.Generate();
        var msUser = User.Faker.Generate();

        await MicrosoftUserRepository.CreateAsync(msUser);
        await AppleUserRepository.CreateAsync(appleUser1);
        await AppleUserRepository.CreateAsync(appleUser2);
        await AppleUserRepository.CreateAsync(appleUser3);

        // Act
        var msUserFromDb = await MicrosoftUserRepository.GetAllAsync();
        var appleUserFromDb = await AppleUserRepository.GetAllAsync();
        AssertPartitionKeyPrefixIsRemoved(msUserFromDb);
        AssertPartitionKeyPrefixIsRemoved(appleUserFromDb);

        // Assert
        msUserFromDb.ShouldBeEquivalentTo(new List<User> { msUser });
        appleUserFromDb.ShouldBeEquivalentTo(new List<User> { appleUser1, appleUser2, appleUser3 });
    }

    [Fact]
    public async Task GetAllAsyncMultiple_ShouldOnlyGetFromCorrectPartitionEvenIfThePartitionKeyIsAlwaysTheSame()
    {
        // Arrange
        await ResetAsync();
        var appleUser1 = User.Faker.Generate();
        var appleUser2 = User.Faker.Generate();
        var appleUser3 = User.Faker.Generate();
        var msUser = User.Faker.Generate();
        appleUser1.TenantId = appleUser2.TenantId = appleUser3.TenantId = msUser.TenantId;
        await MicrosoftUserRepository.CreateAsync(msUser);
        await AppleUserRepository.CreateAsync(appleUser1);
        await AppleUserRepository.CreateAsync(appleUser2);
        await AppleUserRepository.CreateAsync(appleUser3);

        // Act
        var msUserFromDb = await MicrosoftUserRepository.GetAllAsync();
        var appleUserFromDb = await AppleUserRepository.GetAllAsync();
        AssertPartitionKeyPrefixIsRemoved(msUserFromDb);
        AssertPartitionKeyPrefixIsRemoved(appleUserFromDb);

        // Assert
        msUserFromDb.ShouldBeEquivalentTo(new List<User> { msUser });
        appleUserFromDb.ShouldBeEquivalentTo(new List<User> { appleUser1, appleUser2, appleUser3 });
    }
}
