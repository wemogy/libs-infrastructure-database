using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public partial class CosmosMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task GetAsyncMultiple_ShouldGetExistingItemsByIdAndPartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);

        // Act
        var msUserFromDb = await MicrosoftUserRepository.GetAsync(
            user1.Id,
            user1.TenantId);
        var appleUserFromDb = await AppleUserRepository.GetAsync(
            user2.Id,
            user2.TenantId);

        // Assert
        msUserFromDb.Should().BeEquivalentTo(user1);
        appleUserFromDb.Should().BeEquivalentTo(user2);

        var exception1 = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.GetAsync(
                user2.Id,
                user2.TenantId));
        exception1.Should().BeOfType<NotFoundErrorException>();

        var exception2 = await Record.ExceptionAsync(
            () => AppleUserRepository.GetAsync(
                user1.Id,
                user1.TenantId));
        exception1.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task GetAsyncMultiple_ShouldGetExistingItemsById()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);

        // Act
        var msUserFromDb = await MicrosoftUserRepository.GetAsync(user1.Id);
        var appleUserFromDb = await AppleUserRepository.GetAsync(user2.Id);

        // Assert
        msUserFromDb.Should().BeEquivalentTo(user1);
        appleUserFromDb.Should().BeEquivalentTo(user2);

        var exception1 = await Record.ExceptionAsync(() => MicrosoftUserRepository.GetAsync(user2.Id));
        exception1.Should().BeOfType<NotFoundErrorException>();

        var exception2 = await Record.ExceptionAsync(() => AppleUserRepository.GetAsync(user1.Id));
        exception1.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task GetAsyncMultiple_ShouldGetExistingItemsByPredicate()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);

        // Act
        var msUserFromDb = await MicrosoftUserRepository.GetAsync(u => u.Firstname == user1.Firstname);
        var appleUserFromDb = await AppleUserRepository.GetAsync(u => u.Lastname == user2.Lastname);

        // Assert
        msUserFromDb.Should().BeEquivalentTo(user1);
        appleUserFromDb.Should().BeEquivalentTo(user2);

        var exception1 = await Record.ExceptionAsync(() => MicrosoftUserRepository.GetAsync(user2.Id));
        exception1.Should().BeOfType<NotFoundErrorException>();

        var exception2 = await Record.ExceptionAsync(() => AppleUserRepository.GetAsync(user1.Id));
        exception1.Should().BeOfType<NotFoundErrorException>();
    }

    [Fact] // this fails for now
    public async Task GetAsyncMultiple_ShouldGetExistingItemsByPredicate()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        var users = await MicrosoftUserRepository.GetAsync();
        users.Count
        var appleUserFromDb = await AppleUserRepository.GetAsync(u => u.Lastname == user2.Lastname);

        // Assert
        msUserFromDb.Should().BeEquivalentTo(user1);
        appleUserFromDb.Should().BeEquivalentTo(user2);

        var exception1 = await Record.ExceptionAsync(() => MicrosoftUserRepository.GetAsync(user2.Id));
        exception1.Should().BeOfType<NotFoundErrorException>();

        var exception2 = await Record.ExceptionAsync(() => AppleUserRepository.GetAsync(user1.Id));
        exception1.Should().BeOfType<NotFoundErrorException>();
    }
}
