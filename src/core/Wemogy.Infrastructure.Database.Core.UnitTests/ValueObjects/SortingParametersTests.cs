using System.Linq;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;
using SortDirection = Wemogy.Infrastructure.Database.Core.Enums.SortDirection;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.ValueObjects;

public class SortingParametersTests
{
    [Fact]
    public void SortingParameters_OrderBy_HappyPath()
    {
        // Arrange
        var sorting = new Sorting<Animal>();

        // Act
        sorting
            .OrderBy(x => x.Firstname);
        sorting
            .OrderBy(x => x.BestFriend!.Firstname);

        // Assert
        sorting.Parameters.First().Property.ShouldBe("Firstname");
        sorting.Parameters.First().Direction.ShouldBe(SortDirection.Ascending);
        sorting.Parameters[1].Property.ShouldBe("BestFriend.Firstname");
        sorting.Parameters[1].Direction.ShouldBe(SortDirection.Ascending);
    }

    [Fact]
    public void SortingParameters_ApplyTo_HappyPath()
    {
        // Arrange
        var animals = Animal.Faker.Generate(10);
        var sortingParameters = new Sorting<Animal>()
            .OrderBy(x => x.Firstname);

        // Act
        var sortedAnimals = sortingParameters
            .ApplyTo(animals)
            .ToList();

        // Assert
        sortedAnimals.ShouldNotBeNull();
        sortedAnimals.Count.ShouldBe(10);
    }
}
