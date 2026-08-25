using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
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
    public void Deserialize_ShouldScaleAFilterValueOfAFixedPointMember()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.InitializeUsingReflection(typeof(PatchTarget));

        // Act: the property arrives camelCased, and the document carries value * 10^Scale
        var balance = mappingMetadata.Deserialize(
            "balance",
            "12.5");
        var amount = mappingMetadata.Deserialize(
            "inner.amount",
            "1.2345");

        // Assert
        balance.ShouldBe(12500000L);
        amount.ShouldBe(12345L);
    }

    [Fact]
    public void Deserialize_ShouldLeaveADecimalWithoutTheAttributeAlone()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.InitializeUsingReflection(typeof(PatchTarget));

        // Act
        var value = mappingMetadata.Deserialize(
            "money",
            "9.99");

        // Assert
        value.ShouldBe(9.99d);
    }

    [Fact]
    public void Deserialize_ShouldHandTheArrayOfAnIsOneOfFilterThroughUnscaled()
    {
        // Arrange: the query builder re-enters this method once per item, which is where the
        // scaling happens
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.InitializeUsingReflection(typeof(PatchTarget));

        // Act
        var value = mappingMetadata.Deserialize(
            "balance",
            "[0.5,1]");

        // Assert
        value.ShouldBeOfType<JArray>().Count.ShouldBe(2);
    }

    [Fact]
    public void Deserialize_ShouldRefuseAFilterValueItCannotScale()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.InitializeUsingReflection(typeof(PatchTarget));

        // Act & Assert: reported rather than compared unscaled against the stored integer
        var exception = Should.Throw<UnexpectedErrorException>(
            () => mappingMetadata.Deserialize(
                "balance",
                "\"not a number\""));
        exception.Code.ShouldBe("FixedPointFilterValueNotSupported");
    }

    [Fact]
    public void Deserialize_ShouldRefuseAFilterValueFinerThanTheDeclaredScale()
    {
        // Arrange
        var mappingMetadata = new MappingMetadata();
        mappingMetadata.InitializeUsingReflection(typeof(PatchTarget));

        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => mappingMetadata.Deserialize(
                "balance",
                "0.5000001"));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
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
