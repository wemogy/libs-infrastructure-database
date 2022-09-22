using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task GetAsync_ShouldUsePropertyFilter()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        user.PrivateNote = "private note";
        await UserRepository.CreateAsync(user);

        // Act
        var result = await UserRepository.GetAsync(user.Id);

        // Assert
        result.PrivateNote.Should().BeEmpty();
    }

    [Fact]
    public async Task QueryAsync_ShouldUsePropertyFilter()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        user.PrivateNote = "private note";
        await UserRepository.CreateAsync(user);

        // Act
        var result = await UserRepository.QueryAsync(x => x.Id == user.Id);

        // Assert
        result.Should().AllSatisfy(x => x.PrivateNote.Should().BeEmpty());
    }
}
