using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task GetAllAsync_ShouldReturnAllItems()
    {
        // Arrange
        await ResetAsync();
        var users = User.Faker.Generate(20);
        foreach (var user in users)
        {
            await MicrosoftUserRepository.CreateAsync(user);
        }

        // Act
        var usersFromDb = await MicrosoftUserRepository.GetAllAsync();

        // Assert
        usersFromDb.Should().HaveCount(19);
    }

    [Fact]
    public async Task GetAllAsync_ShouldRespectSoftDeleteReturnAllItems()
    {
        // Arrange
        await ResetAsync();
        var users = User.Faker.Generate(20);
        foreach (var user in users)
        {
            if (users.IndexOf(user) % 2 == 0)
            {
                user.IsDeleted = true;
            }

            await MicrosoftUserRepository.CreateAsync(user);
        }

        // Act
        var usersFromDb = await MicrosoftUserRepository.GetAllAsync();

        // Assert
        usersFromDb.Should().HaveCount(9);
    }
}
