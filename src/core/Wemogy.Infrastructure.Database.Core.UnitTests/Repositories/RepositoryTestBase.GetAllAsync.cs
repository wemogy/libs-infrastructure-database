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
        usersFromDb.Should().HaveCount(20);
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
        usersFromDb.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetAllAsync_SoftDeleteShouldNotEnableByDefault()
    {
        // Arrange
        await ResetAsync();
        var dataCenters = DataCenter.Faker.Generate(20);
        foreach (var dataCenter in dataCenters)
        {
            if (dataCenters.IndexOf(dataCenter) % 2 == 0)
            {
                dataCenter.IsDeleted = true;
            }

            await DataCenterRepository.CreateAsync(dataCenter);
        }

        // Act
        var result = await DataCenterRepository.GetAllAsync();

        // Assert
        result.Should().HaveCount(20);
    }
}
