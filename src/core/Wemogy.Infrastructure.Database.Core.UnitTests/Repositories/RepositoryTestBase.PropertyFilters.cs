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
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var result = await MicrosoftUserRepository.GetAsync(user.Id);

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
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var result = await MicrosoftUserRepository.QueryAsync(x => x.Id == user.Id);

        // Assert
        result.Should().AllSatisfy(x => x.PrivateNote.Should().BeEmpty());
    }
}
