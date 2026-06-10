using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Abstractions;

public class DatabaseClientBaseTests
{
    [Fact]
    public void ResolveETagValue_ShouldReturnETagPropertyValue()
    {
        // Arrange
        var client = new TestableDatabaseClient<EntityWithETag>();
        var entity = new EntityWithETag
        {
            ETag = "etag-123"
        };

        // Act
        var eTag = client.ResolveETag(entity);

        // Assert
        eTag.Should().Be("etag-123");
    }

    [Fact]
    public void SetETagValue_ShouldSetETagProperty()
    {
        // Arrange
        var client = new TestableDatabaseClient<EntityWithETag>();
        var entity = new EntityWithETag();

        // Act
        client.SetETag(entity, "etag-456");

        // Assert
        entity.ETag.Should().Be("etag-456");
    }

    [Fact]
    public void SetETagValue_ShouldSupportNull()
    {
        // Arrange
        var client = new TestableDatabaseClient<EntityWithETag>();
        var entity = new EntityWithETag
        {
            ETag = "etag-123"
        };

        // Act
        client.SetETag(entity, null);

        // Assert
        entity.ETag.Should().BeNull();
    }

    [Fact]
    public void ResolveETagValue_ShouldReturnNullForEntityWithoutETagProperty()
    {
        // Arrange
        var client = new TestableDatabaseClient<EntityWithoutETag>();
        var entity = new EntityWithoutETag();

        // Act
        var eTag = client.ResolveETag(entity);

        // Assert
        eTag.Should().BeNull();
    }

    [Fact]
    public void SetETagValue_ShouldBeNoOpForEntityWithoutETagProperty()
    {
        // Arrange
        var client = new TestableDatabaseClient<EntityWithoutETag>();
        var entity = new EntityWithoutETag();

        // Act
        var exception = Record.Exception(() => client.SetETag(entity, "etag-123"));

        // Assert
        exception.Should().BeNull();
    }

    private class EntityWithETag
    {
        [Id]
        public string Id { get; set; } = string.Empty;

        [PartitionKey]
        public string TenantId { get; set; } = string.Empty;

        [ETag]
        public string? ETag { get; set; }
    }

    private class EntityWithoutETag
    {
        [Id]
        public string Id { get; set; } = string.Empty;

        [PartitionKey]
        public string TenantId { get; set; } = string.Empty;
    }

    private class TestableDatabaseClient<TEntity> : DatabaseClientBase<TEntity>
        where TEntity : class
    {
        public string? ResolveETag(TEntity entity)
        {
            return ResolveETagValue(entity);
        }

        public void SetETag(TEntity entity, string? eTag)
        {
            SetETagValue(entity, eTag);
        }
    }
}
