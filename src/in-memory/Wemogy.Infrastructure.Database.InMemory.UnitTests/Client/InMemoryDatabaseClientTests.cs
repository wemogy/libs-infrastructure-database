using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Client;
using Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Client;

/// <summary>
///     Exercises the client directly, against an entity that does not derive from
///     <c>EntityBase</c>. Its store is separate from the one the repository tests use, so these
///     tests cannot interfere with them, but they do share it with each other - hence the reset.
/// </summary>
[Collection("Sequential")]
public class InMemoryDatabaseClientTests
{
    private readonly InMemoryDatabaseClient<KeyedEntity> _client = new InMemoryDatabaseClient<KeyedEntity>();

    public InMemoryDatabaseClientTests()
    {
        _client.DeleteAsync(_ => true).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task GetAsync_ShouldResolveTheIdThroughTheIdAttribute()
    {
        // Arrange: the id property is called "Key", so a lookup that assumes a property named
        // "Id" cannot find it
        var entity = await _client.CreateAsync(Entity("a"));

        // Act
        var fetchedEntity = await _client.GetAsync(
            entity.Key,
            entity.Tenant,
            CancellationToken.None);

        // Assert
        fetchedEntity.Name.ShouldBe("a");
    }

    [Fact]
    public async Task GetAsync_ShouldReturnACopy()
    {
        // Arrange
        var entity = await _client.CreateAsync(Entity("a"));

        // Act: mutating the returned instance must not reach the store
        var fetchedEntity = await _client.GetAsync(
            entity.Key,
            entity.Tenant,
            CancellationToken.None);
        fetchedEntity.Name = "mutated";

        // Assert
        var refetchedEntity = await _client.GetAsync(
            entity.Key,
            entity.Tenant,
            CancellationToken.None);
        refetchedEntity.Name.ShouldBe("a");
    }

    [Fact]
    public async Task IterateAsync_ShouldTakeTheFirstEntitiesNotSkipThem()
    {
        // Arrange: 10 entities and a take of 3, so taking and skipping cannot produce the same
        // count
        await SeedAsync("a", "b", "c", "d", "e", "f", "g", "h", "i", "j");
        var queryParameters = new QueryParameters
        {
            Take = 3,
            Sortings = new List<QuerySorting> { new QuerySorting { OrderBy = "name" } }
        };

        // Act
        var names = await QueryAsync(queryParameters);

        // Assert
        names.ShouldBe(new[] { "a", "b", "c" });
    }

    [Fact]
    public async Task IterateAsync_ShouldReturnEverythingIfTakeIsNotSet()
    {
        // Arrange
        await SeedAsync("a", "b", "c");

        // Act
        var names = await QueryAsync(new QueryParameters());

        // Assert
        names.Length.ShouldBe(3);
    }

    [Fact]
    public async Task IterateAsync_ShouldSortAcrossPartitions()
    {
        // Arrange: one entity per partition, so a sorting applied per partition would be a no-op
        await _client.CreateAsync(Entity("c", tenant: "t3"));
        await _client.CreateAsync(Entity("a", tenant: "t1"));
        await _client.CreateAsync(Entity("b", tenant: "t2"));
        var queryParameters = new QueryParameters
        {
            Sortings = new List<QuerySorting>
            {
                new QuerySorting
                {
                    OrderBy = "name",
                    SortOrder = SortOrder.Descending
                }
            }
        };

        // Act
        var names = await QueryAsync(queryParameters);

        // Assert
        names.ShouldBe(new[] { "c", "b", "a" });
    }

    [Fact]
    public async Task IterateAsync_ShouldApplyTheSearchAfterCursor()
    {
        // Arrange
        await SeedAsync("a", "b", "c", "d");
        var queryParameters = new QueryParameters
        {
            Take = 2,
            Sortings = new List<QuerySorting>
            {
                new QuerySorting
                {
                    OrderBy = "name",
                    SearchAfter = "\"b\""
                }
            }
        };

        // Act
        var names = await QueryAsync(queryParameters);

        // Assert: the page continues after "b" instead of starting over
        names.ShouldBe(new[] { "c", "d" });
    }

    [Fact]
    public async Task IterateAsync_LambdaShouldSortAndPaginateAcrossPartitions()
    {
        // Arrange
        await _client.CreateAsync(Entity("d", tenant: "t4"));
        await _client.CreateAsync(Entity("b", tenant: "t2"));
        await _client.CreateAsync(Entity("a", tenant: "t1"));
        await _client.CreateAsync(Entity("c", tenant: "t3"));

        // Act
        var names = new List<string>();
        await _client.IterateAsync(
            _ => true,
            new Sorting<KeyedEntity>().OrderBy(x => x.Name),
            new Pagination(1, 2),
            entity =>
            {
                names.Add(entity.Name);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert: page 2 of a globally sorted result, not of a per-partition sorted one
        names.ShouldBe(new List<string> { "b", "c" });
    }

    [Fact]
    public async Task IterateAsync_ShouldAllowTheCallbackToWriteToTheStore()
    {
        // Arrange
        await SeedAsync("a", "b");

        // Act: writing to a new partition while iterating used to invalidate the enumerator
        var exception = await Record.ExceptionAsync(
            () => _client.IterateAsync(
                _ => true,
                null,
                null,
                entity => _client.CreateAsync(Entity($"copy-of-{entity.Name}", tenant: "copies")),
                CancellationToken.None));

        // Assert
        exception.ShouldBeNull();
        (await _client.CountAsync(_ => true, CancellationToken.None)).ShouldBe(4);
    }

    [Fact]
    public async Task CountAsync_ShouldCountAcrossPartitions()
    {
        // Arrange
        await _client.CreateAsync(Entity("a", tenant: "t1"));
        await _client.CreateAsync(Entity("b", tenant: "t2"));

        // Act
        var count = await _client.CountAsync(
            _ => true,
            CancellationToken.None);

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateAsync_ShouldRejectADuplicateId()
    {
        // Arrange
        var entity = Entity("a");
        await _client.CreateAsync(entity);

        // Act
        var exception = await Record.ExceptionAsync(() => _client.CreateAsync(entity));

        // Assert
        exception.ShouldBeOfType<ConflictErrorException>();
    }

    [Fact]
    public async Task CreateAsync_ShouldNotAssignAnETagToAnEntityWithoutTheAttribute()
    {
        // Arrange & Act
        var entity = await _client.CreateAsync(Entity("a"));

        // Assert: KeyedEntity does not opt into optimistic concurrency, so nothing may be stamped
        entity.ShouldNotBeNull();
        typeof(KeyedEntity).GetProperty("ETag").ShouldBeNull();
    }

    [Fact]
    public async Task ReplaceAsync_ShouldKeepThePositionOfTheEntity()
    {
        // Arrange
        await SeedAsync("a", "b", "c");
        var entities = await QueryAsync(new QueryParameters());
        var target = await _client.GetAsync(
            "b-0",
            "tenant",
            CancellationToken.None);

        // Act
        target.Name = "b-updated";
        await _client.ReplaceAsync(target);

        // Assert: an unordered iteration keeps the insertion order instead of moving the
        // replaced entity to the end
        var namesAfterReplace = await QueryAsync(new QueryParameters());
        namesAfterReplace.Length.ShouldBe(entities.Length);
        namesAfterReplace[1].ShouldBe("b-updated");
    }

    [Fact]
    public async Task ReplaceAsync_ShouldThrowForAnUnknownEntity()
    {
        // Arrange & Act
        var exception = await Record.ExceptionAsync(() => _client.ReplaceAsync(Entity("a")));

        // Assert
        exception.ShouldBeOfType<NotFoundErrorException>();
    }

    [Fact]
    public async Task UpsertAsync_ShouldCreateAndThenUpdate()
    {
        // Arrange
        var entity = Entity("a");

        // Act
        await _client.UpsertAsync(entity);
        entity.Name = "updated";
        await _client.UpsertAsync(entity);

        // Assert
        (await _client.CountAsync(_ => true, CancellationToken.None)).ShouldBe(1);
        var fetchedEntity = await _client.GetAsync(
            entity.Key,
            entity.Tenant,
            CancellationToken.None);
        fetchedEntity.Name.ShouldBe("updated");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveEveryMatchOfThePredicate()
    {
        // Arrange
        await SeedAsync("a", "b", "c");

        // Act
        await _client.DeleteAsync(x => x.Name != "b");

        // Assert
        var names = await QueryAsync(new QueryParameters());
        names.ShouldBe(new[] { "b" });
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowForAnUnknownEntity()
    {
        // Arrange & Act
        var exception = await Record.ExceptionAsync(() => _client.DeleteAsync("missing", "tenant"));

        // Assert
        exception.ShouldBeOfType<NotFoundErrorException>();
    }

    private static KeyedEntity Entity(string name, int rank = 0, string tenant = "tenant")
    {
        return new KeyedEntity
        {
            Key = $"{name}-{rank}",
            Tenant = tenant,
            Name = name,
            Rank = rank
        };
    }

    private async Task SeedAsync(params string[] names)
    {
        foreach (var name in names)
        {
            await _client.CreateAsync(Entity(name));
        }
    }

    private async Task<string[]> QueryAsync(QueryParameters queryParameters)
    {
        var names = new List<string>();
        await _client.IterateAsync(
            queryParameters,
            null,
            entity =>
            {
                names.Add(entity.Name);
                return Task.CompletedTask;
            },
            CancellationToken.None);
        return names.ToArray();
    }
}
