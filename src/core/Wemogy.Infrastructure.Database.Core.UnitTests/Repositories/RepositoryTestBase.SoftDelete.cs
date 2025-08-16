using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task GetAsync_ShouldThrow_ForSoftDeletedItems()
    {
        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted = true;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act & Assert
        await Should.ThrowAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(
                user.Id,
                user.TenantId));
    }

    [Fact]
    public async Task GetAsync_ShouldReturnSoftDeletedItemWhenSoftDeleteIsDisabled()
    {
        // Arrange
        var user = User.Faker.Generate();
        user.IsDeleted = true;
        MicrosoftUserRepository.SoftDelete.Disable();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userFromDb = await MicrosoftUserRepository.GetAsync(
            user.Id,
            user.TenantId);

        // Assert
        userFromDb.IsDeleted.ShouldBeTrue();
        userFromDb.ShouldBeEquivalentTo(user);
    }
}
