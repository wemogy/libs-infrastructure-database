using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task CreateAsync_ShouldThrowIfTheItemAlreadyExists()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await UserRepository.CreateAsync(user);

        // Act & Assert
        await Assert.ThrowsAsync<ConflictErrorException>(() => UserRepository.CreateAsync(user));
    }
}
