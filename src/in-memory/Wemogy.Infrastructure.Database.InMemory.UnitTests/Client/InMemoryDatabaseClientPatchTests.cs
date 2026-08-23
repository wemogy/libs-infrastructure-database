using System;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.InMemory.Client;
using Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Client;

/// <summary>
///     Patch behaviour that needs a member shape the stored entities of the shared repository
///     tests do not have: a nested value type and a nested reference type.
/// </summary>
[Collection("Sequential")]
public class InMemoryDatabaseClientPatchTests
{
    private readonly InMemoryDatabaseClient<KeyedEntity> _client = new InMemoryDatabaseClient<KeyedEntity>();

    public InMemoryDatabaseClientPatchTests()
    {
        _client.DeleteAsync(_ => true).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task PatchAsync_ShouldWriteThroughANestedValueTypeMember()
    {
        // Arrange: reflection reads a struct member as a boxed copy, so a write to a member of it
        // is lost unless the copy is assigned back
        var entity = await _client.CreateAsync(Entity("a"));

        // Act
        var patchedEntity = await _client.PatchAsync(
            entity.Key,
            entity.Tenant,
            p => p.Increment(x => x.Amount.Minor, 5),
            null,
            CancellationToken.None);

        // Assert
        patchedEntity.Amount.Minor.ShouldBe(5);

        var persistedEntity = await _client.GetAsync(
            entity.Key,
            entity.Tenant,
            CancellationToken.None);
        persistedEntity.Amount.Minor.ShouldBe(5);
    }

    [Fact]
    public async Task PatchAsync_ShouldNotStoreTheInstanceASetWasGiven()
    {
        // Arrange
        var entity = await _client.CreateAsync(Entity("a"));
        var details = new KeyedEntityDetails { Note = "written" };

        // Act
        await _client.PatchAsync(
            entity.Key,
            entity.Tenant,
            p => p.Set(x => x.Details, details),
            null,
            CancellationToken.None);

        // the caller keeps its instance and changes it afterwards
        details.Note = "changed afterwards";

        // Assert: the store is independent of it, like it is for every other write
        var persistedEntity = await _client.GetAsync(
            entity.Key,
            entity.Tenant,
            CancellationToken.None);
        persistedEntity.Details.Note.ShouldBe("written");
    }

    private static KeyedEntity Entity(string name)
    {
        return new KeyedEntity
        {
            Key = Guid.NewGuid().ToString(),
            Tenant = "tenant",
            Name = name
        };
    }
}
