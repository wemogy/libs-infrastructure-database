using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task ExistsByIdMultipleAsync_ShouldWorkIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var msExists = await MicrosoftUserRepository.ExistsAsync(user.Id);
        var appleExists = await AppleUserRepository.ExistsAsync(user.Id);

        // Assert
        msExists.ShouldBeTrue();
        appleExists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsByPredicate_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var msExists = await MicrosoftUserRepository.ExistsAsync(x => x.Id == user.Id && x.TenantId == user.TenantId);
        var appleExists = await AppleUserRepository.ExistsAsync(x => x.Id == user.Id && x.TenantId == user.TenantId);

        // Assert
        msExists.ShouldBeTrue();
        appleExists.ShouldBeFalse();
    }
}
