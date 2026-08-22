using System.Collections.Generic;
using System.Linq;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Query;
using Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Query;

public class InMemoryQueryOrderingTests
{
    [Fact]
    public void ApplySortings_ShouldReturnTheInputUnchangedWithoutSortings()
    {
        // Arrange
        var entities = Entities("c", "a", "b");

        // Act
        var result = InMemoryQueryOrdering.ApplySortings(
            entities,
            new QueryParameters());

        // Assert
        Names(result).ShouldBe(new[] { "c", "a", "b" });
    }

    [Fact]
    public void ApplySortings_ShouldSortAscending()
    {
        // Arrange
        var entities = Entities("c", "a", "b");

        // Act
        var result = InMemoryQueryOrdering.ApplySortings(
            entities,
            ParametersFor(Sorting("name")));

        // Assert
        Names(result).ShouldBe(new[] { "a", "b", "c" });
    }

    [Fact]
    public void ApplySortings_ShouldSortDescending()
    {
        // Arrange
        var entities = Entities("c", "a", "b");

        // Act
        var result = InMemoryQueryOrdering.ApplySortings(
            entities,
            ParametersFor(
                Sorting(
                    "name",
                    SortOrder.Descending)));

        // Assert
        Names(result).ShouldBe(new[] { "c", "b", "a" });
    }

    [Fact]
    public void ApplySortings_ShouldUseFollowUpSortingsAsTieBreaker()
    {
        // Arrange
        var entities = new List<KeyedEntity>
        {
            Entity("a", 2),
            Entity("a", 1),
            Entity("b", 1)
        };

        // Act
        var result = InMemoryQueryOrdering.ApplySortings(
            entities,
            ParametersFor(
                Sorting("name"),
                Sorting("rank")));

        // Assert
        result.Select(x => $"{x.Name}{x.Rank}").ShouldBe(new[] { "a1", "a2", "b1" });
    }

    [Fact]
    public void ApplySearchAfter_ShouldReturnTheInputUnchangedWithoutACursor()
    {
        // Arrange
        var entities = Entities("a", "b");

        // Act
        var result = InMemoryQueryOrdering.ApplySearchAfter(
            entities,
            ParametersFor(Sorting("name")));

        // Assert
        Names(result).ShouldBe(new[] { "a", "b" });
    }

    [Fact]
    public void ApplySearchAfter_ShouldKeepOnlyTheEntitiesAfterTheCursor()
    {
        // Arrange
        var entities = Entities("a", "b", "c");

        // Act
        var result = InMemoryQueryOrdering.ApplySearchAfter(
            entities,
            ParametersFor(
                Sorting(
                    "name",
                    searchAfter: "\"b\"")));

        // Assert: the cursor itself is the last entity of the previous page and must be excluded
        Names(result).ShouldBe(new[] { "c" });
    }

    [Fact]
    public void ApplySearchAfter_ShouldFollowTheDescendingSortOrder()
    {
        // Arrange
        var entities = Entities("a", "b", "c");

        // Act
        var result = InMemoryQueryOrdering.ApplySearchAfter(
            entities,
            ParametersFor(
                Sorting(
                    "name",
                    SortOrder.Descending,
                    "\"b\"")));

        // Assert
        Names(result).ShouldBe(new[] { "a" });
    }

    [Fact]
    public void ApplySearchAfter_ShouldUseFollowUpSortingsAsTieBreaker()
    {
        // Arrange: three entities share the leading sort key, so only the second column can
        // tell the page boundary
        var entities = new List<KeyedEntity>
        {
            Entity("a", 1),
            Entity("a", 2),
            Entity("a", 3),
            Entity("b", 1)
        };

        // Act
        var result = InMemoryQueryOrdering.ApplySearchAfter(
            entities,
            ParametersFor(
                Sorting(
                    "name",
                    searchAfter: "\"a\""),
                Sorting(
                    "rank",
                    searchAfter: "2")));

        // Assert
        result.Select(x => $"{x.Name}{x.Rank}").ShouldBe(new[] { "a3", "b1" });
    }

    [Fact]
    public void ApplySearchAfter_ShouldIgnoreSortingsAfterTheFirstOneWithoutACursor()
    {
        // Arrange
        var entities = new List<KeyedEntity>
        {
            Entity("a", 1),
            Entity("b", 1)
        };

        // Act: only the leading run of sortings with a cursor forms the keyset
        var result = InMemoryQueryOrdering.ApplySearchAfter(
            entities,
            ParametersFor(
                Sorting("name"),
                Sorting(
                    "rank",
                    searchAfter: "5")));

        // Assert
        Names(result).ShouldBe(new[] { "a", "b" });
    }

    private static KeyedEntity Entity(string name, int rank = 0)
    {
        return new KeyedEntity
        {
            Key = $"{name}-{rank}",
            Tenant = "tenant",
            Name = name,
            Rank = rank
        };
    }

    private static List<KeyedEntity> Entities(params string[] names)
    {
        return names.Select(name => Entity(name)).ToList();
    }

    private static string[] Names(IEnumerable<KeyedEntity> entities)
    {
        return entities.Select(x => x.Name).ToArray();
    }

    private static QuerySorting Sorting(
        string orderBy,
        SortOrder sortOrder = SortOrder.Ascending,
        string? searchAfter = null)
    {
        return new QuerySorting
        {
            OrderBy = orderBy,
            SortOrder = sortOrder,
            SearchAfter = searchAfter
        };
    }

    private static QueryParameters ParametersFor(params QuerySorting[] sortings)
    {
        return new QueryParameters { Sortings = new List<QuerySorting>(sortings) };
    }
}
