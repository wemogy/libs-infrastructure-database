using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task ReplaceAsync_ShouldThrowIfTheItemNotExists()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundErrorException>(() => UserRepository.ReplaceAsync(user));
    }
}
