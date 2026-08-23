using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
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
    public void Deserialize_ShouldReturnAJArrayForJsonArrays()
    {
        // Arrange: this is the shape the IsOneOf comparator relies on
        var mappingMetadata = new MappingMetadata();

        // Act
        var value = mappingMetadata.Deserialize(
            "firstname",
            "[\"John\",\"Jane\"]");

        // Assert
        var array = value.ShouldBeOfType<JArray>();
        array.Count.ShouldBe(2);
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

        // Act: only long values are treated as unix timestamps, an ISO string is parsed by
        // Newtonsoft itself and must keep its instant
        var value = mappingMetadata.Deserialize(
            "createdAt",
            "\"2023-01-01T00:00:00Z\"");

        // Assert
        value.ShouldBe(new DateTime(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc));
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
