using System.Linq;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

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
        sorting.Parameters.First().Property.Should().Be("Firstname");
        sorting.Parameters.First().Direction.Should().Be(SortDirection.Ascending);
        sorting.Parameters[1].Property.Should().Be("BestFriend.Firstname");
        sorting.Parameters[1].Direction.Should().Be(SortDirection.Ascending);
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
        sortedAnimals.Should().NotBeNull();
        sortedAnimals.Should().HaveCount(10);
    }
}
