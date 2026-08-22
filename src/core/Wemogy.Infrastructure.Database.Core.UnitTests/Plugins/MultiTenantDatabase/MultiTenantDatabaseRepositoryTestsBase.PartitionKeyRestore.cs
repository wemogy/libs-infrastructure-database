using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task CreateAsync_ShouldRestoreThePartitionKeyWhenTheWriteFails()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        var tenantId = user.TenantId;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act: the second create is rejected, so the tenant prefix has to be rolled back anyway
        await Record.ExceptionAsync(() => MicrosoftUserRepository.CreateAsync(user));

        // Assert: a leftover prefix leaks the tenant into the caller's entity, and a retry would
        // prefix the already prefixed value
        user.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task ReplaceAsync_ShouldRestoreThePartitionKeyWhenTheWriteFails()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        var tenantId = user.TenantId;

        // Act: nothing to replace, so this fails
        await Record.ExceptionAsync(() => MicrosoftUserRepository.ReplaceAsync(user));

        // Assert
        user.TenantId.ShouldBe(tenantId);
    }

    [Fact]
    public async Task UpsertAsync_ShouldStillTargetTheTenantAfterAFailedWrite()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        user.Firstname = "First";
        await MicrosoftUserRepository.CreateAsync(user);

        // a failed write on the same instance, e.g. a transient error a caller retries past
        await Record.ExceptionAsync(() => MicrosoftUserRepository.CreateAsync(user));

        // Act: reusing that instance must still address the tenant's partition. With a leftover
        // prefix the upsert lands in a doubly prefixed partition that no read path composes,
        // so the write silently disappears.
        user.Firstname = "Second";
        await MicrosoftUserRepository.UpsertAsync(user);

        // Assert
        var fetchedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            user.TenantId);
        fetchedUser.Firstname.ShouldBe("Second");
    }

    [Fact]
    public async Task UpsertAsync_ShouldRestoreThePartitionKeyOfTheCallersEntity()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        var tenantId = user.TenantId;

        // Act
        await MicrosoftUserRepository.UpsertAsync(user);

        // Assert: the prefix is an implementation detail of the plugin and must not survive the call
        user.TenantId.ShouldBe(tenantId);
    }
}
