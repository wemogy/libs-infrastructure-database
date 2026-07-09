using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task CreateAsync_ShouldReturnTwoDifferentCreatedEntities()
    {
        // Arrange
        await ResetAsync();
        var msUser = User.Faker.Generate();
        var appleUser = User.Faker.Generate();

        // Act
        var msEntity = await MicrosoftUserRepository.CreateAsync(msUser);
        var appleEntity = await AppleUserRepository.CreateAsync(appleUser);

        // Assert: the returned entities match what is now stored in the DB
        var msReadBack = await MicrosoftUserRepository.GetAsync(msEntity.Id, msEntity.TenantId);
        var appleReadBack = await AppleUserRepository.GetAsync(appleEntity.Id, appleEntity.TenantId);
        msEntity.ShouldBeEquivalentTo(msReadBack);
        appleEntity.ShouldBeEquivalentTo(appleReadBack);
        AssertPartitionKeyPrefixIsRemoved(msEntity);
        AssertPartitionKeyPrefixIsRemoved(appleEntity);
    }
}
