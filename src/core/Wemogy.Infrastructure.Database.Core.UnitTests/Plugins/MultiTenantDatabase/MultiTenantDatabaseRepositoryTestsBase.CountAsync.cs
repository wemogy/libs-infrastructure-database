using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task CountAsync_ShouldReturnTotalCountForeachDatabase()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);

        // Act
        var msUsersCount = await MicrosoftUserRepository.CountAsync(x => true);
        var appleUsersCount = await AppleUserRepository.CountAsync(x => true);

        msUsersCount.Should().Be(2);
        appleUsersCount.Should().Be(1);
    }

    [Fact]
    public async Task CountAsync_ShouldSupportFilteringForeachDatabase()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);

        // Act
        var msUsersCount = await MicrosoftUserRepository.CountAsync(x => x.Id == user1.Id);
        var appleUsersCount = await AppleUserRepository.CountAsync(x => x.Id == user1.Id);

        msUsersCount.Should().Be(1);
        appleUsersCount.Should().Be(1);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnNullIfNoMatchesForeachDatabase()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);

        // Act
        var msUsersCount = await MicrosoftUserRepository.CountAsync(x => false);
        var appleUsersCount = await AppleUserRepository.CountAsync(x => false);

        msUsersCount.Should().Be(0);
        appleUsersCount.Should().Be(0);
    }
}
