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

        // Act & Assert
        msEntity.ShouldBeEquivalentTo(msUser);
        appleEntity.ShouldBeEquivalentTo(appleUser);
        AssertPartitionKeyPrefixIsRemoved(msEntity);
        AssertPartitionKeyPrefixIsRemoved(appleEntity);
    }
}
