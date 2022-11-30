using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task QuerySingleAsync_ShouldGetAnExistingItemByIdWithExpression()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userFromDb = await MicrosoftUserRepository.QuerySingleAsync(x => x.Id == user.Id);

        // Assert
        userFromDb.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldGetAnExistingItemByIdAndPartitionKeyWithExpression()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userFromDb =
            await MicrosoftUserRepository.QuerySingleAsync(x => x.Id == user.Id && x.TenantId == user.TenantId);

        // Assert
        userFromDb.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldThrowWhenMoreThanOneResultsAreReturned()
    {
        // Arrange
        await ResetAsync();
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());
        await MicrosoftUserRepository.CreateAsync(User.Faker.Generate());

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.QuerySingleAsync(x => true));

        // Assert
        exception.Should().BeOfType<PreconditionFailedErrorException>();
    }
}
