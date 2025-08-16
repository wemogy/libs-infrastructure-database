using System.Threading.Tasks;
using Shouldly;
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
        result.PrivateNote.ShouldBeEmpty();
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
        result.ShouldAllBe(x => x.PrivateNote == string.Empty);
    }

    [Fact]
    public async Task GetAllAsync_ShouldUsePropertyFilter()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        user.PrivateNote = "private note";
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var result = await MicrosoftUserRepository.GetAllAsync();

        // Assert
        result.ShouldAllBe(x => x.PrivateNote == string.Empty);
    }

    [Fact]
    public async Task IterateAsync_ShouldUsePropertyFilter()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        user.PrivateNote = "private note";
        await MicrosoftUserRepository.CreateAsync(user);
        var count = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(x => x.Id == user.Id, x =>
        {
            count++;
            x.PrivateNote.ShouldBeEmpty();
        });

        // Assert
        count.ShouldBe(1);
    }
}
