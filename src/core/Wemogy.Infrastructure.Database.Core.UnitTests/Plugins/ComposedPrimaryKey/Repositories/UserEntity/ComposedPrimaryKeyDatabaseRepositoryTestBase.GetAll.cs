using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.Repositories.UserEntity;

public abstract partial class ComposedPrimaryKeyDatabaseRepositoryTestBase
{
    [Fact]
    public async Task GetAllAsync_ShouldRespectComposedKey()
    {
        // Arrange
        var userOtherTenant = User.Faker.Generate();
        SetPrefixContext("tenantA");
        await UserRepository.CreateAsync(userOtherTenant);
        var userMyTenant = User.Faker.Generate();
        SetPrefixContext("tenantB");
        await UserRepository.CreateAsync(userMyTenant);

        // Act
        var allUsers = await UserRepository.GetAllAsync();

        // Assert
        allUsers.Should().NotContain(userOtherTenant);
    }
}
