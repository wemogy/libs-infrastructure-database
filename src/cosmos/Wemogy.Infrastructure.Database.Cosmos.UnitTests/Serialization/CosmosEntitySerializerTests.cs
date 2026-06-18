using System.IO;
using System.Text;
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

    private class EntityWithoutETag
    {
        [Id]
        public string Id { get; set; } = string.Empty;

        public string Firstname { get; set; } = string.Empty;
    }
}
