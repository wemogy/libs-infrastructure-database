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
        var appleUser1 = await AppleUserRepository.CreateAsync(User.Faker.Generate());
        var appleUser2 = await AppleUserRepository.CreateAsync(User.Faker.Generate());
        var appleUser3 = await AppleUserRepository.CreateAsync(User.Faker.Generate());
        var msUser = await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());

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
        var sharedTenantId = User.Faker.Generate().TenantId;
        var msUserRaw = User.Faker.Generate();
        msUserRaw.TenantId = sharedTenantId;
        var appleUser1Raw = User.Faker.Generate();
        appleUser1Raw.TenantId = sharedTenantId;
        var appleUser2Raw = User.Faker.Generate();
        appleUser2Raw.TenantId = sharedTenantId;
        var appleUser3Raw = User.Faker.Generate();
        appleUser3Raw.TenantId = sharedTenantId;
        var msUser = await MicrosoftUserRepository.CreateAsync(msUserRaw);
        var appleUser1 = await AppleUserRepository.CreateAsync(appleUser1Raw);
        var appleUser2 = await AppleUserRepository.CreateAsync(appleUser2Raw);
        var appleUser3 = await AppleUserRepository.CreateAsync(appleUser3Raw);

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
