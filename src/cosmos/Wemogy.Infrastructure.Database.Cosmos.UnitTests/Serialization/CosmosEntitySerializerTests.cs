using System;
using System.IO;
using System.Text;
using System.Text.Json.Serialization;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Cosmos.Serialization;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Serialization;

public class CosmosEntitySerializerTests
{
    private readonly CosmosEntitySerializer _serializer = new CosmosEntitySerializer();

    [Fact]
    public void ToStream_ShouldNotSerializeETagProperty()
    {
        // Arrange
        var entity = new EntityWithETag
        {
            Id = "1",
            TenantId = "tenant",
            Firstname = "John",
            ETag = "\"some-etag\""
        };

        // Act
        var json = ToJson(entity);

        // Assert: the eTag must never end up in the persisted document body
        json.ShouldNotContain("_etag");
        json.ShouldNotContain("some-etag");
    }

    [Fact]
    public void ToStream_ShouldUseCamelCaseForRegularProperties()
    {
        // Arrange
        var entity = new EntityWithETag
        {
            Id = "1",
            TenantId = "tenant",
            Firstname = "John"
        };

        // Act
        var json = ToJson(entity);

        // Assert
        json.ShouldContain("\"id\":\"1\"");
        json.ShouldContain("\"tenantId\":\"tenant\"");
        json.ShouldContain("\"firstname\":\"John\"");
    }

    [Fact]
    public void FromStream_ShouldPopulateETagFromUnderscoreEtagField()
    {
        // Arrange
        var json = "{\"id\":\"1\",\"tenantId\":\"tenant\",\"firstname\":\"John\",\"_etag\":\"\\\"abc-123\\\"\"}";

        // Act
        var entity = FromJson<EntityWithETag>(json);

        // Assert: the eTag must be read back from Cosmos' system "_etag" field
        entity.ETag.ShouldBe("\"abc-123\"");
        entity.Firstname.ShouldBe("John");
    }

    [Fact]
    public void FromStream_ShouldIgnoreUnderscoreEtagForEntityWithoutETagProperty()
    {
        // Arrange
        var json = "{\"id\":\"1\",\"firstname\":\"John\",\"_etag\":\"\\\"abc-123\\\"\"}";

        // Act
        var entity = FromJson<EntityWithoutETag>(json);

        // Assert
        entity.Firstname.ShouldBe("John");
    }

    [Fact]
    public void SerializeMemberName_ShouldReturnUnderscoreEtagForETagProperty()
    {
        // Arrange
        var member = typeof(EntityWithETag).GetProperty(nameof(EntityWithETag.ETag))!;

        // Act
        var name = _serializer.SerializeMemberName(member);

        // Assert
        name.ShouldBe("_etag");
    }

    [Fact]
    public void SerializeMemberName_ShouldReturnCamelCaseForRegularProperty()
    {
        // Arrange
        var member = typeof(EntityWithETag).GetProperty(nameof(EntityWithETag.TenantId))!;

        // Act
        var name = _serializer.SerializeMemberName(member);

        // Assert
        name.ShouldBe("tenantId");
    }

    [Fact]
    public void ToStream_ShouldWriteAZeroOffsetTimestampWithTheZSuffix()
    {
        // Arrange
        var entity = new EntityWithTimestamps
        {
            Id = "1",
            CreatedAt = new DateTimeOffset(2026, 8, 25, 10, 0, 0, TimeSpan.Zero)
        };

        // Act
        var json = ToJson(entity);

        // Assert: Cosmos DB orders a timestamp as the string it is stored as, and "+" (0x2B) and
        // "Z" (0x5A) interleave wrongly under that comparison. A container written by both this
        // version and one that still typed the field as a UTC DateTime therefore only stays
        // sortable while the spelling matches, which is what this pins.
        json.ShouldContain("\"createdAt\":\"2026-08-25T10:00:00Z\"");
        json.ShouldNotContain("+00:00");
    }

    [Fact]
    public void ToStream_ShouldWriteATimestampTheSameWayItWasWrittenAsAUtcDateTime()
    {
        // Arrange: the same instant, in the type the field had before and the one it has now
        var instant = new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc);
        var before = new EntityWithLegacyTimestamp { Id = "1", CreatedAt = instant };
        var after = new EntityWithTimestamps { Id = "1", CreatedAt = new DateTimeOffset(instant) };

        // Act
        var jsonBefore = ToJson(before);
        var jsonAfter = ToJson(after);

        // Assert: byte for byte, so an upgrade does not split one instant into two spellings
        jsonAfter.ShouldBe(jsonBefore);
    }

    [Fact]
    public void ToStream_ShouldKeepAnOffsetTheConsumerChoseToStore()
    {
        // Arrange
        var entity = new EntityWithTimestamps
        {
            Id = "1",
            CreatedAt = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.FromHours(2))
        };

        // Act
        var json = ToJson(entity);

        // Assert: only a zero offset is respelled. Normalizing a real offset away would be a
        // silent loss of what the consumer stored rather than a compatibility fix.
        json.ShouldContain("\"createdAt\":\"2026-08-25T12:00:00+02:00\"");
    }

    [Theory]
    [InlineData("2026-08-25T10:00:00Z")]
    [InlineData("2026-08-25T10:00:00+00:00")]
    [InlineData("2026-08-25T12:00:00+02:00")]
    public void FromStream_ShouldReadEitherSpellingOfTheSameInstant(string stored)
    {
        // Arrange
        var json = $"{{\"id\":\"1\",\"createdAt\":\"{stored}\"}}";

        // Act
        var entity = FromJson<EntityWithTimestamps>(json);

        // Assert: a document written before the upgrade has to keep reading back correctly
        entity.CreatedAt.UtcDateTime.ShouldBe(new DateTime(2026, 8, 25, 10, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void ToStream_ShouldOmitNullValues()
    {
        // Arrange
        var entity = new EntityWithETag { Id = "1", Firstname = null! };

        // Act
        var json = ToJson(entity);

        // Assert
        json.ShouldNotContain("firstname");
    }

    [Fact]
    public void ToStream_ShouldNotEscapeNonAsciiCharacters()
    {
        // Arrange
        var entity = new EntityWithETag { Id = "1", Firstname = "Jürgen" };

        // Act
        var json = ToJson(entity);

        // Assert: a Cosmos document is not an HTML context, and escaping every non-ASCII
        // character would make a document unreadable and unequal to what was stored before
        json.ShouldContain("\"firstname\":\"Jürgen\"");
    }

    [Fact]
    public void ToStream_ShouldHonourAJsonPropertyNameAttribute()
    {
        // Arrange
        var entity = new EntityWithTimestamps { Id = "1", Label = "some label" };

        // Act
        var json = ToJson(entity);

        // Assert
        json.ShouldContain("\"customLabel\":\"some label\"");
    }

    [Fact]
    public void SerializeMemberName_ShouldHonourAJsonPropertyNameAttribute()
    {
        // Arrange: a patch path and a LINQ query both resolve a member through this
        var member = typeof(EntityWithTimestamps).GetProperty(nameof(EntityWithTimestamps.Label))!;

        // Act
        var name = _serializer.SerializeMemberName(member);

        // Assert
        name.ShouldBe("customLabel");
    }

    [Fact]
    public void FromStream_ShouldMatchAPropertyNameCaseInsensitively()
    {
        // Arrange: what the Newtonsoft.Json based serializer did, and what a document written by
        // an older version of a consumer may well rely on
        var json = "{\"Id\":\"1\",\"FIRSTNAME\":\"John\"}";

        // Act
        var entity = FromJson<EntityWithETag>(json);

        // Assert
        entity.Id.ShouldBe("1");
        entity.Firstname.ShouldBe("John");
    }

    [Fact]
    public void ToStream_ShouldSerializeByTheRuntimeType()
    {
        // Arrange: the SDK hands a query parameter over as an object, and serializing that by its
        // declared type would write an empty document and silently drop the filter value
        object entity = new EntityWithETag { Id = "1", Firstname = "John" };

        // Act
        var json = ToJson(entity);

        // Assert
        json.ShouldContain("\"firstname\":\"John\"");
    }

    private string ToJson<T>(T input)
    {
        using var stream = _serializer.ToStream(input);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private T FromJson<T>(string json)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _serializer.FromStream<T>(stream);
    }

    private class EntityWithETag
    {
        [Id]
        public string Id { get; set; } = string.Empty;

        [PartitionKey]
        public string TenantId { get; set; } = string.Empty;

        public string Firstname { get; set; } = string.Empty;

        [ETag]
        public string? ETag { get; init; }
    }

    private class EntityWithTimestamps
    {
        [Id]
        public string Id { get; set; } = string.Empty;

        public DateTimeOffset CreatedAt { get; set; }

        [JsonPropertyName("customLabel")]
        public string? Label { get; set; }
    }

    private class EntityWithLegacyTimestamp
    {
        [Id]
        public string Id { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("customLabel")]
        public string? Label { get; set; }
    }

    private class EntityWithoutETag
    {
        [Id]
        public string Id { get; set; } = string.Empty;

        public string Firstname { get; set; } = string.Empty;
    }
}
