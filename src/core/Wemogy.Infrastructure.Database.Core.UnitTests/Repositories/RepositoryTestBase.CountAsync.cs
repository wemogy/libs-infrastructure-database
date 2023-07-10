using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task CountAsync_ShouldCountAllItems()
    {
        // Arrange
        var totalUserCount = 10;
        await ResetAsync();
        for (int i = 0; i < totalUserCount; i++)
        {
            var user = User.Faker.Generate();
            await MicrosoftUserRepository.CreateAsync(user);
        }

        // Act
        var userCount = await MicrosoftUserRepository.CountAsync(x => true);

        // Assert
        userCount.Should().Be(totalUserCount);
    }

    [Fact]
    public async Task CountAsync_ShouldCountAllMatchedItems()
    {
        // Arrange
        var totalUserCount = 10;
        var firstUserId = Guid.NewGuid().ToString();
        await ResetAsync();
        for (int i = 0; i < totalUserCount; i++)
        {
            var user = User.Faker.Generate();

            if (i == 0)
            {
                user.Id = firstUserId;
            }

            await MicrosoftUserRepository.CreateAsync(user);
        }

        // Act
        var userCount = await MicrosoftUserRepository.CountAsync(x => x.Id == firstUserId);

        // Assert
        userCount.Should().Be(1);
    }

    [Fact]
    public async Task CountAsync_ShouldReturnNullIfNoMatches()
    {
        // Arrange
        var totalUserCount = 10;
        await ResetAsync();
        for (int i = 0; i < totalUserCount; i++)
        {
            var user = User.Faker.Generate();
            await MicrosoftUserRepository.CreateAsync(user);
        }

        // Act
        var userCount = await MicrosoftUserRepository.CountAsync(x => false);

        // Assert
        userCount.Should().Be(0);
    }
}
