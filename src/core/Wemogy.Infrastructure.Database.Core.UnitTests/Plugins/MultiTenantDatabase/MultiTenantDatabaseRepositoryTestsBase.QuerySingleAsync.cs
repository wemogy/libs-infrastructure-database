using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task QuerySingleAsync_ShouldGetExistingItemsByPredicate()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);

        // Act
        var msUserFromDb = await MicrosoftUserRepository.QuerySingleAsync(u => u.Firstname == user1.Firstname);
        var appleUserFromDb = await AppleUserRepository.QuerySingleAsync(u => u.Lastname == user2.Lastname);

        // Assert
        msUserFromDb.Should().BeEquivalentTo(user1);
        appleUserFromDb.Should().BeEquivalentTo(user2);
        AssertPartitionKeyPrefixIsRemoved(msUserFromDb);
        AssertPartitionKeyPrefixIsRemoved(appleUserFromDb);

        var exception1 = await Record.ExceptionAsync(() => MicrosoftUserRepository.GetAsync(user2.Id));
        exception1.Should().BeOfType<NotFoundErrorException>();
        AssertExceptionMessageDoesNotContainPrefix(exception1);

        var exception2 = await Record.ExceptionAsync(() => AppleUserRepository.GetAsync(user1.Id));
        exception2.Should().BeOfType<NotFoundErrorException>();
        AssertExceptionMessageDoesNotContainPrefix(exception2);
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldGetExistingItemsByPredicateForPartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);

        // Act
        var msUserFromDb = await MicrosoftUserRepository.QuerySingleAsync(u => u.TenantId == user1.TenantId);
        var appleUserFromDb =
            await AppleUserRepository.QuerySingleAsync(u => u.TenantId == user2.TenantId && u.Id == user2.Id);
        AssertPartitionKeyPrefixIsRemoved(msUserFromDb);
        AssertPartitionKeyPrefixIsRemoved(appleUserFromDb);

        // Assert
        msUserFromDb.Should().BeEquivalentTo(user1);
        appleUserFromDb.Should().BeEquivalentTo(user2);
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldGetExistingItemsByPredicateForSamePartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        user1.Firstname = "MS";
        var user2 = User.Faker.Generate();
        user2.TenantId = user1.TenantId; // fake same tenantId
        user2.Firstname = "APPLE";
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);

        // Act
        var msUserFromDb = await MicrosoftUserRepository.QuerySingleAsync(u => u.Firstname == user1.Firstname);
        var appleUserFromDb = await AppleUserRepository.QuerySingleAsync(u => u.Lastname == user2.Lastname);

        // Assert
        msUserFromDb.Should().BeEquivalentTo(user1);
        appleUserFromDb.Should().BeEquivalentTo(user2);
        AssertPartitionKeyPrefixIsRemoved(msUserFromDb);
        AssertPartitionKeyPrefixIsRemoved(appleUserFromDb);

        var exception1 = await Record.ExceptionAsync(() => MicrosoftUserRepository.GetAsync(user2.Id));
        exception1.Should().BeOfType<NotFoundErrorException>();
        AssertExceptionMessageDoesNotContainPrefix(exception1);

        var exception2 = await Record.ExceptionAsync(() => AppleUserRepository.GetAsync(user1.Id));
        exception2.Should().BeOfType<NotFoundErrorException>();
        AssertExceptionMessageDoesNotContainPrefix(exception2);
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldGetExistingItemsByPredicateWithPartitionKeyFilterForSamePartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        user1.Firstname = "MS";
        var user2 = User.Faker.Generate();
        user2.TenantId = user1.TenantId; // fake same tenantId
        user2.Firstname = "APPLE";
        await MicrosoftUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);

        // Act - TODO: PartitionKey not supported
        var msUserFromDb = await MicrosoftUserRepository.QuerySingleAsync(u => u.TenantId == user1.TenantId);
        var appleUserFromDb =
            await AppleUserRepository.QuerySingleAsync(u => u.TenantId == user2.TenantId && u.Id == user2.Id);
        AssertPartitionKeyPrefixIsRemoved(msUserFromDb);
        AssertPartitionKeyPrefixIsRemoved(appleUserFromDb);

        // Assert
        msUserFromDb.Should().BeEquivalentTo(user1);
        appleUserFromDb.Should().BeEquivalentTo(user2);
    }
}
