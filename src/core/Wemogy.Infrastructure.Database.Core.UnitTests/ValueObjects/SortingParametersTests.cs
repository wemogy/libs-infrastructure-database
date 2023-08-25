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
        var sortingParameters = new SortingParameters<Animal>();

        // Act
        sortingParameters
            .AddOrderBy(x => x.Firstname);
        sortingParameters
            .AddOrderBy(x => x.BestFriend!.Firstname);

        // Assert
        sortingParameters.First().Property.Should().Be("Firstname");
        sortingParameters.First().Direction.Should().Be(SortDirection.Ascending);
        sortingParameters[1].Property.Should().Be("BestFriend.Firstname");
        sortingParameters[1].Direction.Should().Be(SortDirection.Ascending);
    }

    [Fact]
    public void SortingParameters_ApplyTo_HappyPath()
    {
        // Arrange
        var animals = Animal.Faker.Generate(10);
        var sortingParameters = new SortingParameters<Animal>()
            .AddOrderBy(x => x.Firstname);

        // Act
        var sortedAnimals = sortingParameters
            .ApplyTo(animals)
            .ToList();

        // Assert
        sortedAnimals.Should().NotBeNull();
        sortedAnimals.Should().HaveCount(10);
    }
}
