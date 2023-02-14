using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task IterateAsync_ShouldGetAnExistingItemByIdWithExpression()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        var count = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(x => x.Id == user.Id, x =>
        {
            count++;
        });

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task IterateAsync_ShouldGetAnExistingItemByPartitionKeyWithExpression()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        var count = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(x => x.TenantId == user.TenantId, x =>
        {
            count++;
        });

        // Assert
        count.Should().Be(1);
    }

    [Fact]
    public async Task IterateAsync_ShouldWorkWithAsynchronousCallback()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);
        var count = 0;

        // Act
        await MicrosoftUserRepository.IterateAsync(x => x.TenantId == user.TenantId, (x) =>
        {
            count++;

            return Task.CompletedTask;
        });

        // Assert
        count.Should().Be(1);
    }
}
