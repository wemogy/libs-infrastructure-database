using System;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task GetAsync_ShouldGetAnExistingItemById()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userFromDb = await MicrosoftUserRepository.GetAsync(user.Id);

        // Assert
        userFromDb.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task GetAsync_ShouldThrowIfItemNotFound()
    {
        // Arrange
        await ResetAsync();

        // Act & Assert
        await Assert.ThrowsAsync<NotFoundErrorException>(
            () => MicrosoftUserRepository.GetAsync(Guid.NewGuid().ToString()));
    }

    [Fact]
    public async Task GetAsync_ShouldGetAnExistingItemByIdAndPartitionKey()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var userFromDb = await MicrosoftUserRepository.GetAsync(
            user.Id,
            user.TenantId);

        // Assert
        userFromDb.Should().BeEquivalentTo(user);
    }

    [Fact]
    public async Task GetAsync_ShouldThrowIfIdOrPartitionKeyNotFound()
    {
        // Arrange
        await ResetAsync();
        var id = Guid.NewGuid().ToString();
        var partitionKey = Guid.NewGuid().ToString();
        var notFoundException = DatabaseError.EntityNotFound(
            id,
            partitionKey);

        // Act
        var exception = await Record.ExceptionAsync(
            () => MicrosoftUserRepository.GetAsync(
                id,
                partitionKey));

        // Act & Assert
        exception.Should().BeOfType<NotFoundErrorException>()
            .Which.Code.Should().Be(notFoundException.Code);
        exception.Should().BeOfType<NotFoundErrorException>()
            .Which.Description.Should().Be(notFoundException.Description);
    }
}
