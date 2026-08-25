using System;
using System.Collections.Generic;
using Shouldly;
using Wemogy.Infrastructure.Database.Cosmos.Models;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Models;

public class MappingMetadataTests
{
    [Fact]
    public void Deserialize_ShouldReturnTheDeserializedStringValue()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "firstname",
            "\"John\"");

        // Assert
        value.ShouldBe("John");
    }

    [Fact]
    public void Deserialize_ShouldReturnTheDeserializedNumericValue()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "age",
            "30");

        // Assert
        value.ShouldBe(30L);
    }

    [Fact]
    public void Deserialize_ShouldReturnAListForJsonArrays()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "firstname",
            "[\"John\",\"Jane\"]");

        // Assert: a plain CLR list, because the client of the container serializes the query
        // parameter and cannot be handed a node of the document it was parsed from
        var array = value.ShouldBeOfType<List<object?>>();
        array.ShouldBe(new object?[] { "John", "Jane" });
    }

    [Fact]
    public void DeserializeArray_ShouldReturnOneValuePerElement()
    {
        // Arrange: this is the shape the IsOneOf comparator relies on, which needs a parameter
        // per element rather than one parameter holding the whole array
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.DeserializeArray(
            "firstname",
            "[\"John\",\"Jane\"]");

        // Assert
        value.ShouldBe(new object?[] { "John", "Jane" });
    }

    [Fact]
    public void DeserializeArray_ShouldApplyTheMappingToEveryElement()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.AddCustomMappings(
            new Dictionary<string, Type> { { "createdAt", typeof(DateTime) } });

        // Act
        var value = mappingMetadata.DeserializeArray(
            "createdAt",
            "[0,1000000]");

        // Assert
        value.ShouldBe(
            new object?[]
            {
                new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(1970, 1, 1, 0, 16, 40, DateTimeKind.Utc)
            });
    }

    [Fact]
    public void DeserializeArray_ShouldReturnNullForJsonThatIsNotAnArray()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.DeserializeArray(
            "firstname",
            "\"John\"");

        // Assert: the caller reports the filter as unusable rather than building a condition
        value.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_ShouldParseATimestampSoTheClientCanRespellIt()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "updatedAt",
            "\"2026-08-25T10:00:00+00:00\"");

        // Assert: handing the raw string to the query would compare "…+00:00" against documents
        // stored as "…Z", and "+" (0x2B) sorts before "Z" (0x5A) - an ascending search-after
        // cursor would hand back the last row of the previous page. As a timestamp, the client of
        // the container writes it in the same spelling the document was written with.
        value.ShouldBe(new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Deserialize_ShouldKeepAStringThatIsNotATimestamp()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "firstname",
            "\"2026 was a good year\"");

        // Assert
        value.ShouldBe("2026 was a good year");
    }

    [Fact]
    public void Deserialize_ShouldReturnNullForJsonNull()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "firstname",
            "null");

        // Assert: the query builder turns this into an IS_NULL / IS_DEFINED condition
        value.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_ShouldFallBackToTheRawValueForInvalidJson()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "firstname",
            "John");

        // Assert
        value.ShouldBe("John");
    }

    [Fact]
    public void Deserialize_ShouldConvertUnixTimestampsForDateTimeMappings()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.AddCustomMappings(
            new Dictionary<string, Type> { { "createdAt", typeof(DateTime) } });

        // Act
        var epoch = mappingMetadata.Deserialize(
            "createdAt",
            "0");
        var later = mappingMetadata.Deserialize(
            "createdAt",
            "1000000");

        // Assert: the timestamps are milliseconds since the epoch. Pinned exactly, because a
        // switch to seconds would shift every date filter by a factor of 1000 while still
        // producing a plausible-looking DateTime.
        epoch.ShouldBe(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        later.ShouldBe(new DateTime(1970, 1, 1, 0, 16, 40, DateTimeKind.Utc));
    }

    [Fact]
    public void Deserialize_ShouldMatchCustomMappingsCaseInsensitive()
    {
        // Arrange: filters arrive camelCased, custom mappings may be registered PascalCased
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.AddCustomMappings(
            new Dictionary<string, Type> { { "CreatedAt", typeof(DateTime) } });

        // Act
        var value = mappingMetadata.Deserialize(
            "createdAt",
            "0");

        // Assert
        value.ShouldBeOfType<DateTime>();
    }

    [Fact]
    public void Deserialize_ShouldNotApplyTheEpochConversionToAnIsoDateString()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.AddCustomMappings(
            new Dictionary<string, Type> { { "createdAt", typeof(DateTime) } });

        // Act: only a number is treated as a unix timestamp
        var value = mappingMetadata.Deserialize(
            "createdAt",
            "\"2023-01-01T00:00:00Z\"");

        // Assert
        value.ShouldBe(new DateTimeOffset(2023, 1, 1, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Deserialize_ShouldNotConvertUnixTimestampsForUnmappedProperties()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "createdAt",
            "1000000");

        // Assert
        value.ShouldBe(1000000L);
    }

    [Fact]
    public void AddCustomMappings_ShouldBeAdditive()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();

        // Act
        mappingMetadata.AddCustomMappings(
            new Dictionary<string, Type> { { "createdAt", typeof(DateTime) } });
        mappingMetadata.AddCustomMappings(
            new Dictionary<string, Type> { { "updatedAt", typeof(DateTime) } });

        // Assert
        mappingMetadata.Deserialize(
                "createdAt",
                "0")
            .ShouldBeOfType<DateTime>();
        mappingMetadata.Deserialize(
                "updatedAt",
                "0")
            .ShouldBeOfType<DateTime>();
    }
}
