using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.Repositories.AnimalEntity;

public abstract partial class ComposedPrimaryKeyDatabaseRepositoryTestBase
{
    [Fact]
    public async Task GetAsync_ShouldRespectComposedKey()
    {
        // Arrange
        var animal = Animal.Faker.Generate();
        var prefix = "tenantA";
        SetPrefixContext(prefix);
        await AnimalRepository.CreateAsync(animal);

        // Act
        var animalFromDb = await AnimalRepository.GetAsync(
            animal.Id,
            animal.TenantId);

        // Assert
        animalFromDb.Should().BeEquivalentTo(animal);
    }
}
