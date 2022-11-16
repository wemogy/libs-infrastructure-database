using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public partial class CosmosMultiTenantDatabaseRepositoryTests
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

        var msUser = await MicrosoftUserRepository.GetAllAsync();
        msUser.First().Should().BeEquivalentTo(user1);
        var appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().Should().BeEquivalentTo(user2);

        var updatedUser = User.Faker.Generate();
        updatedUser.Id = user1Id;
        updatedUser.TenantId = user1TenantId;

        // Act
        var msFinalUser = await MicrosoftUserRepository.ReplaceAsync(updatedUser);

        // Assert
        msFinalUser.Should().BeEquivalentTo(updatedUser);
        msUser = await MicrosoftUserRepository.GetAllAsync();
        msUser.First().Should().BeEquivalentTo(updatedUser);

        appleUser = await AppleUserRepository.GetAllAsync();
        appleUser.First().Should().BeEquivalentTo(user2); // should not update
    }
}
