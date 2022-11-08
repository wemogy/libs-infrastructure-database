using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public partial class CosmosMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task DeleteAsync_ShouldDeleteFromCorrectTenant()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();

        // Act
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);
        var msEntities = await MicrosoftUserRepository.GetAllAsync();
        var appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.Should().Be(2);
        appleEntities.Count.Should().Be(2);

        // Act
        await MicrosoftUserRepository.DeleteAsync(user1.Id);
        await AppleUserRepository.DeleteAsync(user2.Id);

        // Assert
        msEntities = await MicrosoftUserRepository.GetAllAsync();
        appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.Should().Be(1);
        msEntities.Should().ContainSingle(u => u.Id == user2.Id);
        appleEntities.Count.Should().Be(1);
        appleEntities.Should().ContainSingle(u => u.Id == user1.Id);
    }
}
