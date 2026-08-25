using System;
using System.Text.Json;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Serialization;
using Xunit;
using SortDirection = Wemogy.Infrastructure.Database.Core.Enums.SortDirection;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Serialization;

/// <summary>
///     The JSON a caller writes into a filter value or a search-after cursor is read with these
///     options, and query building has no try/catch around it - a spelling that stops being
///     accepted throws at the call site rather than filtering nothing.
/// </summary>
public class DatabaseJsonTests
{
    [Theory]
    [InlineData("\"2026-08-25T10:00:00Z\"")]
    [InlineData("\"2026-08-25T10:00:00+00:00\"")]
    public void QueryValueOptions_ShouldReadTheIsoDateSpellings(string json)
    {
        // Act
        var value = JsonSerializer.Deserialize<DateTimeOffset>(
            json,
            DatabaseJson.QueryValueOptions);

        // Assert
        value.ShouldBe(new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero));
    }

    [Theory]
    [InlineData("\"2026-08-25 10:00:00\"")]
    [InlineData("\"08/25/2026 10:00:00\"")]
    public void QueryValueOptions_ShouldReadTheDateSpellingsOnlyNewtonsoftAccepted(string json)
    {
        // Act
        var value = JsonSerializer.Deserialize<DateTimeOffset>(
            json,
            DatabaseJson.QueryValueOptions);

        // Assert: the wall clock as written. A spelling carrying no offset is read in the zone of
        // the running machine, which is what Newtonsoft.Json did with it too - asserting the UTC
        // instant instead would make this test depend on where it runs.
        value.DateTime.ShouldBe(new DateTime(2026, 8, 25, 10, 0, 0));
    }

    [Theory]
    [InlineData("\"2026-08-25 10:00:00\"")]
    [InlineData("\"08/25/2026 10:00:00\"")]
    public void QueryValueOptions_ShouldReadTheSameSpellingsIntoADateTime(string json)
    {
        // Act
        var value = JsonSerializer.Deserialize<DateTime>(
            json,
            DatabaseJson.QueryValueOptions);

        // Assert
        value.ShouldBe(new DateTime(2026, 8, 25, 10, 0, 0));
    }

    [Theory]
    [InlineData("\"Ascending\"", SortDirection.Ascending)]
    [InlineData("0", SortDirection.Ascending)]
    [InlineData("\"Descending\"", SortDirection.Descending)]
    [InlineData("1", SortDirection.Descending)]
    public void QueryValueOptions_ShouldReadAnEnumByNameAndByNumber(string json, SortDirection expected)
    {
        // Act
        var value = JsonSerializer.Deserialize<SortDirection>(
            json,
            DatabaseJson.QueryValueOptions);

        // Assert: System.Text.Json rejects the name on its own
        value.ShouldBe(expected);
    }

    [Fact]
    public void QueryValueOptions_ShouldReadANumberInsideAString()
    {
        // Act
        var value = JsonSerializer.Deserialize<long>(
            "\"30\"",
            DatabaseJson.QueryValueOptions);

        // Assert
        value.ShouldBe(30L);
    }
}
