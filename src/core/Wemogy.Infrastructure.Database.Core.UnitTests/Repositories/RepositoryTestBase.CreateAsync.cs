using System.Threading.Tasks;
using Shouldly;
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
        await MicrosoftUserRepository.CreateAsync(user);

        // Act & Assert
        await Should.ThrowAsync<ConflictErrorException>(() => MicrosoftUserRepository.CreateAsync(user));
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnCreatedEntity()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        var entity = await MicrosoftUserRepository.CreateAsync(user);

        // Act & Assert
        entity.ShouldBeEquivalentTo(user);
    }
}
