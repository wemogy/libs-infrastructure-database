using System.Threading.Tasks;
using FluentAssertions;
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

        msEntities.Count.Should().Be(2);
        appleEntities.Count.Should().Be(2);

        // Act
        await MicrosoftUserRepository.DeleteAsync(user1.Id);
        await AppleUserRepository.DeleteAsync(user2.Id);

        // Assert
        msEntities = await MicrosoftUserRepository.GetAllAsync();
        appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.Should().Be(1);
        msEntities.Should().ContainSingle(u => u.Id == user2.Id);
        appleEntities.Count.Should().Be(1);
        appleEntities.Should().ContainSingle(u => u.Id == user1.Id);
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

        msEntities.Count.Should().Be(2);
        appleEntities.Count.Should().Be(2);

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

        msEntities.Count.Should().Be(1);
        msEntities.Should().ContainSingle(u => u.Id == user2.Id);
        appleEntities.Count.Should().Be(1);
        appleEntities.Should().ContainSingle(u => u.Id == user1.Id);
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

        msEntities.Count.Should().Be(2);
        appleEntities.Count.Should().Be(2);

        // Act
        await MicrosoftUserRepository.DeleteAsync(u => u.Firstname == user1.Firstname);
        await AppleUserRepository.DeleteAsync(u => u.Lastname == user2.Lastname);

        // Assert
        msEntities = await MicrosoftUserRepository.GetAllAsync();
        appleEntities = await AppleUserRepository.GetAllAsync();

        msEntities.Count.Should().Be(1);
        msEntities.Should().ContainSingle(u => u.Id == user2.Id);
        appleEntities.Count.Should().Be(1);
        appleEntities.Should().ContainSingle(u => u.Id == user1.Id);
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
        var count = await MicrosoftUserRepository.DeleteAsync(u => u.TenantId == user2.TenantId && u.Id == user2.Id);

        // Assert
        var msEntities = await MicrosoftUserRepository.GetAllAsync();

        msEntities.Should().HaveCount(1);
        msEntities.Should().ContainSingle(u => u.Id == user1.Id);
        count.Should().Be(1);
    }

    [Fact]
    public async Task DeleteByIdAndPartitionKeyShouldThrowIfNotExists()
    {
        // Arrange
        await ResetAsync();

        // Act
        var exception1 = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync(
                "123",
                "tenantId"));

        // Assert
        exception1.Should().BeOfType<NotFoundErrorException>();
        AssertExceptionMessageDoesNotContainPrefix(exception1);
    }

    [Fact]
    public async Task DeleteByIdShouldThrowIfNotExists()
    {
        // Arrange
        await ResetAsync();

        // Act
        var exception1 = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.DeleteAsync("123"));

        // Assert
        exception1.Should().BeOfType<NotFoundErrorException>();
        AssertExceptionMessageDoesNotContainPrefix(exception1);
    }

    [Fact]
    public async Task MultitenantDeleteAllAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        await AppleUserRepository.CreateAsync(User.Faker.Generate());
        await AppleUserRepository.CreateAsync(User.Faker.Generate());
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());

        // Act
        var count = await MicrosoftUserRepository.DeleteAsync(x => true);

        // Assert
        var entities = await MicrosoftUserRepository.QueryAsync(x => true);
        entities.Should().BeEmpty();
        count.Should().Be(4);

        var appleEntities = await AppleUserRepository.QueryAsync(x => true);
        appleEntities.Count.Should().Be(2);
    }
}
