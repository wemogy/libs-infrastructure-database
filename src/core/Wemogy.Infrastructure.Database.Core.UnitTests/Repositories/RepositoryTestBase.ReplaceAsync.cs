using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    [Fact]
    public async Task ReplaceAsync_ShouldThrowIfTheItemNotExists()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();

        // Act & Assert
        await Should.ThrowAsync<NotFoundErrorException>(() => MicrosoftUserRepository.ReplaceAsync(user));
    }

    [Fact]
    public async Task ReplaceAsync_ShouldWork()
    {
        // Arrange
        await ResetAsync();
        var user = User.Faker.Generate();
        var id = user.Id;
        var tenantId = user.TenantId;
        var created = await MicrosoftUserRepository.CreateAsync(user);
        created.TenantId.ShouldBe(user.TenantId);

        var updatedUser = User.Faker.Generate();
        updatedUser.Id = id;
        updatedUser.TenantId = tenantId;

        // Act
        var finalUser = await MicrosoftUserRepository.ReplaceAsync(updatedUser);

        finalUser.ShouldBeEquivalentTo(updatedUser);
    }
}
