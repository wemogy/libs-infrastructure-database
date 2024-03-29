using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task GetAsync_ShouldThrowNotFoundIfReadFilteredItem()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        user.Firstname = "John";
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var exception = await Record.ExceptionAsync(() => MicrosoftUserRepository.GetAsync(user.Id));

        // Assert
        exception.Should().NotBeNull();
        exception.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task QueryAsync_ShouldNotReturnReadFilteredItem()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        user1.Firstname = "John";
        await MicrosoftUserRepository.CreateAsync(user1);
        var user2 = User.Faker.Generate();
        user2.Firstname = "Not John";
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        var result = await MicrosoftUserRepository.QueryAsync(x => true);

        // Assert
        result.Should().HaveCount(1);
        result.First().Firstname.Should().Be(user2.Firstname);
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnReadFilteredItem()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        user1.Firstname = "John";
        await MicrosoftUserRepository.CreateAsync(user1);
        var user2 = User.Faker.Generate();
        user2.Firstname = "Not John";
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        var result = await MicrosoftUserRepository.GetAllAsync();

        // Assert
        result.Should().HaveCount(1);
        result.First().Firstname.Should().Be(user2.Firstname);
    }

    [Fact]
    public async Task IterateAsync_ShouldNotIterateReadFilteredItem()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        user1.Firstname = "John";
        await MicrosoftUserRepository.CreateAsync(user1);
        var user2 = User.Faker.Generate();
        user2.Firstname = "Not John";
        await MicrosoftUserRepository.CreateAsync(user2);
        var count = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(x => true, x =>
        {
            count++;
            x.Firstname.Should().NotBe(user1.Firstname);
        });

        // Assert
        count.Should().Be(1);
    }
}
