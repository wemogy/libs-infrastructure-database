using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTests
{
    [Fact]
    public void Test()
    {
        // Arrange
        var multiTenantRepo = new MultiTenantDatabaseRepository<User>(UserRepository);
        var user = User.Faker.Generate();

        // Act
        multiTenantRepo.CreateAsync(user);
        var x = multiTenantRepo.GetAsync(
            user.Id,
            $"a_{user.TenantId}");
    }
}
