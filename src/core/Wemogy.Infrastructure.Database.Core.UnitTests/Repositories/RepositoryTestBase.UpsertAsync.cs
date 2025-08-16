using System;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task UpsertAsync_ShouldCreateIfNotExist()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        Exception? exception = null;
        User? updatedUser = null;

        try
        {
            updatedUser = await MicrosoftUserRepository.UpsertAsync(
                user,
                user.TenantId);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert
        if (exception is NotSupportedException)
        {
            // Expected outcome in certain implementations
            return;
        }

        exception.ShouldBeNull();
        updatedUser.ShouldNotBeNull();
        updatedUser.Id.ShouldBe(user.Id);
        updatedUser.TenantId.ShouldBe(user.TenantId);
    }

    [Fact]
    public async Task UpsertAsync_ShouldReplaceIfExist()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        user.Firstname = "Updated";

        // Act
        Exception? exception = null;
        User? updatedUser = null;

        try
        {
            updatedUser = await MicrosoftUserRepository.UpsertAsync(
                user,
                user.TenantId);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert
        if (exception is NotSupportedException)
        {
            // Expected outcome in certain implementations
            return;
        }

        exception.ShouldBeNull();
        updatedUser.ShouldNotBeNull();
        updatedUser.Firstname.ShouldBe("Updated");
        updatedUser.Id.ShouldBe(user.Id);
        updatedUser.TenantId.ShouldBe(user.TenantId);
    }

    [Fact]
    public async Task UpsertAsyncWithoutPartitionKey_ShouldCreateIfNotExist()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act
        Exception? exception = null;
        User? updatedUser = null;

        try
        {
            updatedUser = await MicrosoftUserRepository.UpsertAsync(user);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert
        if (exception is NotSupportedException)
        {
            // Expected outcome in certain implementations
            return;
        }

        exception.ShouldBeNull();
        updatedUser.ShouldNotBeNull();
        updatedUser.Id.ShouldBe(user.Id);
        updatedUser.TenantId.ShouldBe(user.TenantId);
    }

    [Fact]
    public async Task UpsertAsyncWithoutPartitionKey_ShouldReplaceIfExist()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        user.Firstname = "Updated";

        // Act
        Exception? exception = null;
        User? updatedUser = null;

        try
        {
            updatedUser = await MicrosoftUserRepository.UpsertAsync(user);
        }
        catch (Exception ex)
        {
            exception = ex;
        }

        // Assert
        if (exception is NotSupportedException)
        {
            // Expected outcome in certain implementations
            return;
        }

        exception.ShouldBeNull();
        updatedUser.ShouldNotBeNull();
        updatedUser.Firstname.ShouldBe("Updated");
        updatedUser.Id.ShouldBe(user.Id);
        updatedUser.TenantId.ShouldBe(user.TenantId);
    }
}
