using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTests
{
    [Fact]
    public async Task TestCreateAsync()
    {
        // Arrange
        var user = User.Faker.Generate();

        // Act
        var actual = await _multiTenantRepo.CreateAsync(user);

        // Assert
        actual.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task TestGetAsync()
    {
        // Arrange
        var user = User.Faker.Generate();
        await _multiTenantRepo.CreateAsync(user);

        // Act
        var actual = await _multiTenantRepo.GetAsync(
            user.Id,
            user.TenantId);

        // Assert
        actual.Should().BeEquivalentTo(user);
    }
}
