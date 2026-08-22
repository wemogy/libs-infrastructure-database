using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task UpsertAsync_ShouldCreateTheEntityInsideTheTenant()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        var upsertedUser = await MicrosoftUserRepository.UpsertAsync(user);

        // Assert: an upsert that does not scope to the tenant writes somewhere the tenant cannot
        // read from afterwards
        upsertedUser.ShouldBeEquivalentToIgnoringETag(user);
        AssertPartitionKeyPrefixIsRemoved(upsertedUser);

        var msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.Count.ShouldBe(1);
        msUsers.First().ShouldBeEquivalentToIgnoringETag(user);
    }

    [Fact]
    public async Task UpsertAsync_ShouldUpdateTheEntityInsideTheTenant()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.UpsertAsync(user);

        // Act
        user.Firstname = "Updated";
        await MicrosoftUserRepository.UpsertAsync(user);

        // Assert: the second upsert has to hit the same partition as the first one
        var msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.Count.ShouldBe(1);
        msUsers.First().Firstname.ShouldBe("Updated");
    }

    [Fact]
    public async Task UpsertAsync_ShouldNotBeVisibleToAnotherTenant()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        await MicrosoftUserRepository.UpsertAsync(user);

        // Assert
        var appleUsers = await AppleUserRepository.GetAllAsync();
        appleUsers.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_ShouldNotOverwriteTheEntityOfAnotherTenant()
    {
        // Arrange: both tenants upsert an entity with the same id and the same logical partition
        // key. Without tenant scoping they collide in one partition and one overwrites the other.
        await ResetAsync();
        var msUser = User.Faker.Generate();
        msUser.Firstname = "Microsoft";
        var appleUser = User.Faker
            .RuleFor(x => x.Id, msUser.Id)
            .Generate();
        appleUser.TenantId = msUser.TenantId;
        appleUser.Firstname = "Apple";

        // Act
        await MicrosoftUserRepository.UpsertAsync(msUser);
        await AppleUserRepository.UpsertAsync(appleUser);

        // Assert
        var msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.Count.ShouldBe(1);
        msUsers.First().Firstname.ShouldBe("Microsoft");

        var appleUsers = await AppleUserRepository.GetAllAsync();
        appleUsers.Count.ShouldBe(1);
        appleUsers.First().Firstname.ShouldBe("Apple");
    }

    [Fact]
    public async Task UpsertAsync_WithPartitionKey_ShouldCreateTheEntityInsideTheTenant()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        var upsertedUser = await MicrosoftUserRepository.UpsertAsync(
            user,
            user.TenantId);

        // Assert
        AssertPartitionKeyPrefixIsRemoved(upsertedUser);

        var msUsers = await MicrosoftUserRepository.GetAllAsync();
        msUsers.Count.ShouldBe(1);
        msUsers.First().ShouldBeEquivalentToIgnoringETag(user);
    }

    [Fact]
    public async Task UpsertAsync_WithPartitionKey_ShouldNotBeVisibleToAnotherTenant()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        await MicrosoftUserRepository.UpsertAsync(
            user,
            user.TenantId);

        // Assert
        var appleUsers = await AppleUserRepository.GetAllAsync();
        appleUsers.ShouldBeEmpty();
    }

    [Fact]
    public async Task UpsertAsync_ShouldBeReadableByIdAndPartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        await MicrosoftUserRepository.UpsertAsync(user);

        // Assert: GetAsync composes the partition key, so an unscoped upsert would not be found
        var fetchedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            user.TenantId);
        fetchedUser.ShouldBeEquivalentToIgnoringETag(user);
    }
}
