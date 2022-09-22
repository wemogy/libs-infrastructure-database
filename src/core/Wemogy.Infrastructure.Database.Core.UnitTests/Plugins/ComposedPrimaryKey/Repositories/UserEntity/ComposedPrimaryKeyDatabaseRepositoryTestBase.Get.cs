using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.Repositories.UserEntity;

public abstract partial class ComposedPrimaryKeyDatabaseRepositoryTestBase
{
    [Fact]
    public async Task GetAsync_ShouldRespectComposedKey()
    {
        // Arrange
        var user = User.Faker.Generate();
        var prefix = "tenantA";
        SetPrefixContext(prefix);
        await UserRepository.CreateAsync(user);

        // Act
        var userFromDb = await UserRepository.GetAsync(
            user.Id,
            user.TenantId);

        // Assert
        userFromDb.Should().BeEquivalentTo(user);
    }
}
