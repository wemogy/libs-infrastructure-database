using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task GetByIdsAsync_ShouldReturnAllItems()
    {
        // Arrange
        await ResetAsync();
        var users = User.Faker.Generate(20);
        foreach (var user in users)
        {
            await UserRepository.CreateAsync(user);
        }

        var ids = users.Take(5).Select(x => x.Id).ToList();

        // Act
        var usersFromDb = await UserRepository.GetByIdsAsync(ids);

        // Assert
        usersFromDb.Should().HaveCount(5);
    }
}
