using System;
using System.Text.Json;
using Shouldly;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Serialization;

/// <summary>
///     The timestamps of <see cref="EntityBase"/> were a <see cref="DateTime"/> before v5, and a
///     <see cref="DateTime"/> does not carry its <see cref="DateTime.Kind"/> in its value - so a
///     document written from one whose Kind was <see cref="DateTimeKind.Unspecified"/> carries no
///     offset at all. Reading that back has to mean the same instant everywhere.
/// </summary>
public class UtcDateTimeOffsetJsonConverterTests
{
    private static readonly DateTimeOffset TenUtc = new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    ///     A caller's own options rather than the ones this library configures - the point of the
    ///     attribute is that it holds without them. camelCase because that is how the document was
    ///     written.
    /// </summary>
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Read_ShouldTakeAStoredValueWithoutAnOffsetAsUtc()
    {
        // Arrange: what Newtonsoft.Json wrote for a DateTime of Kind.Unspecified
        var json = "{\"createdAt\":\"2026-08-25T10:00:00\"}";

        // Act
        var entity = JsonSerializer.Deserialize<TimestampEntity>(json, Options)!;

        // Assert: without the converter this does not throw - it silently takes the offset of the
        // reading machine, so the same document means one instant in Berlin and another in a UTC
        // container. That is the bug the move to DateTimeOffset exists to remove.
        entity.CreatedAt.ShouldBe(TenUtc);
        entity.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Read_ShouldTakeAStoredValueWithSubSecondsButNoOffsetAsUtc()
    {
        // Arrange
        var json = "{\"createdAt\":\"2026-08-25T10:00:00.1234567\"}";

        // Act
        var entity = JsonSerializer.Deserialize<TimestampEntity>(json, Options)!;

        // Assert
        entity.CreatedAt.ShouldBe(TenUtc.AddTicks(1234567));
        entity.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void Read_ShouldKeepAStoredZeroOffset()
    {
        // Arrange: what a UTC DateTime was written as, and what v5 writes
        var json = "{\"createdAt\":\"2026-08-25T10:00:00Z\"}";

        // Act
        var entity = JsonSerializer.Deserialize<TimestampEntity>(json, Options)!;

        // Assert
        entity.CreatedAt.ShouldBe(TenUtc);
    }

    [Fact]
    public void Read_ShouldKeepAStoredOffset()
    {
        // Arrange
        var json = "{\"createdAt\":\"2026-08-25T12:00:00+02:00\"}";

        // Act
        var entity = JsonSerializer.Deserialize<TimestampEntity>(json, Options)!;

        // Assert: the same instant, and the offset the document carried
        entity.CreatedAt.ShouldBe(TenUtc);
        entity.CreatedAt.Offset.ShouldBe(TimeSpan.FromHours(2));
    }

    [Fact]
    public void Read_ShouldTakeANumberAsMillisecondsSinceTheUnixEpoch()
    {
        // Arrange: the shape Wemogy.Core writes a DateTime in, which System.Text.Json refuses to
        // read into a DateTimeOffset on its own
        var json = "{\"createdAt\":1787652000000}";

        // Act
        var entity = JsonSerializer.Deserialize<TimestampEntity>(json, Options)!;

        // Assert
        entity.CreatedAt.ShouldBe(TenUtc);
    }

    [Fact]
    public void Read_ShouldThrowAJsonExceptionForAValueThatIsNotATimestamp()
    {
        // Arrange
        var json = "{\"createdAt\":\"not a timestamp\"}";

        // Act & Assert: a JsonException rather than a FormatException, so a caller catching the
        // deserializer's own exception type still catches this
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<TimestampEntity>(json, Options));
    }

    [Fact]
    public void Write_ShouldSpellAZeroOffsetWithTheZSuffix()
    {
        // Arrange
        var entity = new TimestampEntity { CreatedAt = TenUtc, UpdatedAt = TenUtc };

        // Act
        var json = JsonSerializer.Serialize(entity, Options);

        // Assert: the property attribute has to carry the write side too, because it takes
        // precedence over a converter registered on the options
        json.ShouldContain("\"createdAt\":\"2026-08-25T10:00:00Z\"");
        json.ShouldNotContain("+00:00");
    }

    [Fact]
    public void Clone_ShouldRoundTripATimestampThroughTheWemogyCoreOptions()
    {
        // Arrange: the in-memory provider copies an entity with Wemogy.Core's Clone, which uses
        // its own JsonSerializerOptions - the attribute is what makes the timestamps survive a
        // serializer this library does not configure
        var entity = new TimestampEntity { CreatedAt = TenUtc, UpdatedAt = TenUtc.AddHours(3) };

        // Act
        var clone = entity.Clone();

        // Assert
        clone.CreatedAt.ShouldBe(entity.CreatedAt);
        clone.UpdatedAt.ShouldBe(entity.UpdatedAt);
        clone.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void EntityBase_ShouldCarryTheAttributeOnBothTimestamps()
    {
        // Assert: an entity deriving from EntityBase gets the behaviour without opting in
        typeof(EntityBase).GetProperty(nameof(EntityBase.CreatedAt))!
            .IsDefined(typeof(UtcDateTimeOffsetAttribute), true).ShouldBeTrue();
        typeof(EntityBase).GetProperty(nameof(EntityBase.UpdatedAt))!
            .IsDefined(typeof(UtcDateTimeOffsetAttribute), true).ShouldBeTrue();
    }

    private class TimestampEntity : EntityBase
    {
    }
}
