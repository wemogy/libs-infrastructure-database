using System;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
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

    [Fact]
    public async Task PatchAsync_ShouldNotLetTheConditionChangeWhatIsStored()
    {
        // Arrange: this provider compiles conditions in process, so a condition can call a method -
        // and that method must not be able to write to the store while deciding
        var entity = await _client.CreateAsync(Entity("a"));

        // Act
        await Should.ThrowAsync<ConflictErrorException>(
            () => _client.PatchAsync(
                entity.Key,
                entity.Tenant,
                p => p.Set(x => x.Name, "patched"),
                x => RenameAndDeny(x),
                CancellationToken.None));

        // Assert: neither the operation nor the condition left anything behind
        var persistedEntity = await _client.GetAsync(
            entity.Key,
            entity.Tenant,
            CancellationToken.None);
        persistedEntity.Name.ShouldBe("a");
    }

    [Fact]
    public async Task PatchAsync_ShouldNotTouchTheStoreWhenTheTokenIsAlreadyCancelled()
    {
        // Arrange
        var entity = await _client.CreateAsync(Entity("a"));
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();

        // Act
        await Should.ThrowAsync<OperationCanceledException>(
            () => _client.PatchAsync(
                entity.Key,
                entity.Tenant,
                p => p.Set(x => x.Name, "patched"),
                null,
                cancellationTokenSource.Token));

        // Assert
        var persistedEntity = await _client.GetAsync(
            entity.Key,
            entity.Tenant,
            CancellationToken.None);
        persistedEntity.Name.ShouldBe("a");
    }

    private static bool RenameAndDeny(KeyedEntity entity)
    {
        entity.Name = "written by the condition";
        return false;
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
