using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

public partial class CosmosMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
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

        // Assert
        exception.Should().BeNull();
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

        // Assert
        exception.Should().BeNull();
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

        // Assert
        exception.Should().BeNull();
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

        // Assert
        exception.Should().BeNull();
    }
}
