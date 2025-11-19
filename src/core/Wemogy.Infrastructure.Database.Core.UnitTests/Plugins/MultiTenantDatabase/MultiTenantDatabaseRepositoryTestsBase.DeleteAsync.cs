using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task DeleteByIdAsync_ShouldDeleteFromCorrectTenant()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();

        // Act
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);
        var msEntities = await MicrosoftUserRepository.GetAllAsync();
        var appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.ShouldBe(2);
        appleEntities.Count.ShouldBe(2);

        // Act
        await MicrosoftUserRepository.DeleteAsync(user1.Id);
        await AppleUserRepository.DeleteAsync(user2.Id);

        // Assert
        msEntities = await MicrosoftUserRepository.GetAllAsync();
        appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.ShouldBe(1);
        msEntities.ShouldContain(u => u.Id == user2.Id, 1);
        appleEntities.Count.ShouldBe(1);
        appleEntities.ShouldContain(u => u.Id == user1.Id, 1);
    }

    [Fact]
    public async Task DeleteByIdAndTenantIdAsync_ShouldDeleteFromCorrectTenant()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();

        // Act
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);
        var msEntities = await MicrosoftUserRepository.GetAllAsync();
        var appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.ShouldBe(2);
        appleEntities.Count.ShouldBe(2);

        // Act
        await MicrosoftUserRepository.DeleteAsync(
            user1.Id,
            user1.TenantId);
        await AppleUserRepository.DeleteAsync(
            user2.Id,
            user2.TenantId);

        // Assert
        msEntities = await MicrosoftUserRepository.GetAllAsync();
        appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.ShouldBe(1);
        msEntities.ShouldContain(u => u.Id == user2.Id, 1);
        appleEntities.Count.ShouldBe(1);
        appleEntities.ShouldContain(u => u.Id == user1.Id, 1);
    }

    [Fact]
    public async Task DeleteByPredicateAsync_ShouldDeleteFromCorrectTenant()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        var user2 = User.Faker.Generate();

        // Act
        await MicrosoftUserRepository.CreateAsync(user1);
        await MicrosoftUserRepository.CreateAsync(user2);
        await AppleUserRepository.CreateAsync(user1);
        await AppleUserRepository.CreateAsync(user2);
        var msEntities = await MicrosoftUserRepository.GetAllAsync();
        var appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.ShouldBe(2);
        appleEntities.Count.ShouldBe(2);

        // Act
        await MicrosoftUserRepository.DeleteAsync(u => u.Firstname == user1.Firstname);
        await AppleUserRepository.DeleteAsync(u => u.Lastname == user2.Lastname);

        // Assert
        msEntities = await MicrosoftUserRepository.GetAllAsync();
        appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.ShouldBe(1);
        msEntities.ShouldContain(u => u.Id == user2.Id, 1);
        appleEntities.Count.ShouldBe(1);
        appleEntities.ShouldContain(u => u.Id == user1.Id, 1);
    }

    [Fact]
    public async Task DeleteByPredicateForPartitionKeyAsync_ShouldDeleteFromCorrectTenant()
    {
        // Arrange
        await ResetAsync();
        var user1 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user1);
        var user2 = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user2);

        // Act
        await MicrosoftUserRepository.DeleteAsync(u => u.TenantId == user2.TenantId && u.Id == user2.Id);

        // Assert
        var msEntities = await MicrosoftUserRepository.GetAllAsync();

        msEntities.ShouldHaveSingleItem();
        msEntities.ShouldContain(u => u.Id == user1.Id, 1);
    }

    [Fact]
    public async Task DeleteShouldThrowIfNotExists()
    {
        // TODO: This is inconsistent to the other DeleteAsync behaviours -> DeleteAsync(id) does not throw.

        // Arrange
        await ResetAsync();

        // Act
        var exception1 = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync(
                "123",
                "tenantId"));

        // Assert
        exception1.ShouldBeOfType<NotFoundErrorException>();
        AssertExceptionMessageDoesNotContainPrefix(exception1);
    }
}
