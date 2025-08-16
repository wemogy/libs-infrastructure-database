using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract partial class MultiTenantDatabaseRepositoryTestsBase
{
    [Fact]
    public async Task EnsureExistsByIdMultipleAsync_ShouldWorkIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var exception = await Record.ExceptionAsync(() => MicrosoftUserRepository.EnsureExistsAsync(user.Id));
        AssertExceptionMessageDoesNotContainPrefix(exception);

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public async Task EnsureExistsByPredicateMultipleAsync_ShouldWorkIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var exception =
            await Record.ExceptionAsync(
                () => MicrosoftUserRepository.EnsureExistsAsync(u => u.Id == user.Id && u.Firstname == user.Firstname));
        AssertExceptionMessageDoesNotContainPrefix(exception);

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public async Task EnsureExistsByIdAndPartitionKeyMultipleAsync_ShouldWorkIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var exception =
            await Record.ExceptionAsync(
                () => MicrosoftUserRepository.EnsureExistsAsync(
                    user.Id,
                    user.TenantId));
        AssertExceptionMessageDoesNotContainPrefix(exception);

        // Assert
        exception.ShouldBeNull();
    }

    [Fact]
    public async Task EnsureExistsByPredicateForPartitionKeyMultipleAsync_ShouldWorkIfItemWasFound()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act - TODO: Predicate does not support the PartitionKey
        var exception =
            await Record.ExceptionAsync(
                () => MicrosoftUserRepository.EnsureExistsAsync(u => u.TenantId == user.TenantId));
        AssertExceptionMessageDoesNotContainPrefix(exception);

        // Assert
        exception.ShouldBeNull();
    }
}
