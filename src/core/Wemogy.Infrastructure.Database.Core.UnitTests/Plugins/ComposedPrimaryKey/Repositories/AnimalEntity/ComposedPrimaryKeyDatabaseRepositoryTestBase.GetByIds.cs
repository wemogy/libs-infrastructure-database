using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.Repositories.AnimalEntity;

public abstract partial class ComposedPrimaryKeyDatabaseRepositoryTestBase
{
    [Fact]
    public async Task GetByIdsAsync_ShouldRespectComposedKey()
    {
        // Arrange
        var animal = Animal.Faker.Generate();
        var prefix = "tenantA";
        SetPrefixContext(prefix);
        await AnimalRepository.CreateAsync(animal);

        // Act
        var animalFromDb = await AnimalRepository.GetByIdsAsync(
            new List<string>() { animal.Id });

        // Assert
        animalFromDb.Should().HaveCount(1);
        animalFromDb[0].Should().BeEquivalentTo(animal);
    }
}
