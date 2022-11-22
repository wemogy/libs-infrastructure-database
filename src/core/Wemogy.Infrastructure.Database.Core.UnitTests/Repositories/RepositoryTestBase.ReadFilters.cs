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
}
