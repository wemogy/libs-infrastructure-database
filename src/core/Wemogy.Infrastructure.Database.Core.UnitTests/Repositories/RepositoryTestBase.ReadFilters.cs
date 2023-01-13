using System.Linq;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task GetAsync_ShouldThrowNotFoundIfEntityIsFilteredOut()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        user.Firstname = "John";
        await MicrosoftUserRepository.CreateAsync(user);

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(
                user.Id,
                user.TenantId));
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnFilteredItem()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        user1.Firstname = "John";
        await MicrosoftUserRepository.CreateAsync(user1);
        var user2 = User.Faker.Generate();
        user2.Firstname = "Not John";
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        var result = await MicrosoftUserRepository.GetAllAsync();

        // Act & Assert
        Assert.Single(result);
        Assert.NotEqual(user1.Firstname, result.First().Firstname);
    }
}
