using System;
using System.Threading.Tasks;
using FluentAssertions;
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

        // Act & Assert
        if (MicrosoftUserRepository.GetType().Name.Contains("Mongo"))
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await MicrosoftUserRepository.UpsertAsync(user, user.TenantId));
        }
        else
        {
            var updatedUser = await MicrosoftUserRepository.UpsertAsync(
                user,
                user.TenantId);
            updatedUser.Id.Should().Be(user.Id);
            updatedUser.TenantId.Should().Be(user.TenantId);
        }
    }

    [Fact]
    public async Task UpsertAsync_ShouldReplaceIfExist()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        user.Firstname = "Updated";

        // Act & Assert
        if (MicrosoftUserRepository.GetType().Name.Contains("Mongo"))
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await MicrosoftUserRepository.UpsertAsync(user, user.TenantId));
        }
        else
        {
            var updatedUser = await MicrosoftUserRepository.UpsertAsync(
                user,
                user.TenantId);
            updatedUser.Firstname.Should().Be("Updated");
            updatedUser.Id.Should().Be(user.Id);
            updatedUser.TenantId.Should().Be(user.TenantId);
        }
    }

    [Fact]
    public async Task UpsertAsyncWithoutPartitionKey_ShouldCreateIfNotExist()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act & Assert
        if (MicrosoftUserRepository.GetType().Name.Contains("Mongo"))
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await MicrosoftUserRepository.UpsertAsync(user));
        }
        else
        {
            var updatedUser = await MicrosoftUserRepository.UpsertAsync(user);
            updatedUser.Id.Should().Be(user.Id);
            updatedUser.TenantId.Should().Be(user.TenantId);
        }
    }

    [Fact]
    public async Task UpsertAsyncWithoutPartitionKey_ShouldReplaceIfExist()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        user.Firstname = "Updated";

        // Act & Assert
        if (MicrosoftUserRepository.GetType().Name.Contains("Mongo"))
        {
            await Assert.ThrowsAsync<NotSupportedException>(async () =>
                await MicrosoftUserRepository.UpsertAsync(user));
        }
        else
        {
            var updatedUser = await MicrosoftUserRepository.UpsertAsync(user);
            updatedUser.Firstname.Should().Be("Updated");
            updatedUser.Id.Should().Be(user.Id);
            updatedUser.TenantId.Should().Be(user.TenantId);
        }
    }
}
