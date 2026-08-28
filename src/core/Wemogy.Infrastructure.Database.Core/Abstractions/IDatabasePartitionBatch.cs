using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     A set of write operations against a single logical partition of a single container that is
///     applied atomically: either every operation succeeds, or none of them is applied.
///     <para>
///         Unlike <see cref="IDatabaseTransactionalBatch{TEntity}"/>, the operations of a partition
///         batch are not tied to one entity type: each names its own type, so a create of one shape
///         and a conditional patch of another can travel together as long as they share the
///         partition and live in the container the batch was started against. Cosmos DB's native
///         <c>TransactionalBatch</c> works the same way.
///     </para>
///     <para>
///         A batch is single-use and not thread-safe: build it from one thread, execute it once.
///         Read filters, property filters and soft-delete are not applied to its operations, the
///         same way <c>CreateAsync</c> and <c>ReplaceAsync</c> pass straight through to the provider.
///     </para>
/// </summary>
public interface IDatabasePartitionBatch
{
    /// <summary>
    ///     The number of operations added so far.
    /// </summary>
    int OperationCount { get; }

    /// <summary>
    ///     Creates a new entity. Fails the whole batch if an entity with the same id already exists
    ///     in the batch's partition.
    /// </summary>
    /// <param name="entity">The entity to create</param>
    /// <typeparam name="T">The type of the entity, which names how it is stored in the container</typeparam>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabasePartitionBatch Create<T>(T entity)
        where T : class;

    /// <summary>
    ///     Replaces an existing entity. If the entity opts into optimistic concurrency via
    ///     <see cref="Attributes.ETagAttribute"/> and carries an eTag, it is sent as a precondition
    ///     and a mismatch fails the whole batch.
    /// </summary>
    /// <param name="entity">The updated entity which will replace the existing one</param>
    /// <typeparam name="T">The type of the entity, which names how it is stored in the container</typeparam>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabasePartitionBatch Replace<T>(T entity)
        where T : class;

    /// <summary>
    ///     Inserts or updates an entity. Carries no precondition, mirroring <c>UpsertAsync</c>.
    /// </summary>
    /// <param name="entity">The entity to insert or update</param>
    /// <typeparam name="T">The type of the entity, which names how it is stored in the container</typeparam>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabasePartitionBatch Upsert<T>(T entity)
        where T : class;

    /// <summary>
    ///     Deletes the entity with the given id from the batch's partition. This is a hard delete,
    ///     consistent with <c>DeleteAsync</c>; a soft delete is a <see cref="Replace{T}"/> with the
    ///     flag set. The type is named so the error a missing entity raises can name its shape, the
    ///     same way it does for the typed batch.
    /// </summary>
    /// <param name="id">The id of the entity to delete</param>
    /// <typeparam name="T">The type of the entity to delete</typeparam>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabasePartitionBatch Delete<T>(string id)
        where T : class;

    /// <summary>
    ///     Applies a partial update to the document with the given id, optionally only if the
    ///     condition holds. A condition that does not hold fails the <em>whole</em> batch with a
    ///     <see cref="Wemogy.Core.Errors.Exceptions.ConflictErrorException"/> and the code
    ///     <c>PatchConditionNotMet</c>, which a caller can tell apart from the stale eTag of a
    ///     <see cref="Replace{T}"/> in the same batch.
    /// </summary>
    /// <param name="id">The id of the document to patch</param>
    /// <param name="operations">Adds the operations to apply, e.g. <c>p => p.Increment(x => x.Value, 1)</c></param>
    /// <param name="condition">An optional condition that has to hold for the patch to be applied</param>
    /// <typeparam name="T">The type of the document to patch</typeparam>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabasePartitionBatch Patch<T>(
        string id,
        Action<IPatchOperations<T>> operations,
        Expression<Func<T, bool>>? condition = null)
        where T : class;

    /// <summary>
    ///     Executes every operation atomically. A batch with no operations completes without
    ///     touching the database.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the execution</param>
    /// <returns>A task that completes when every operation has been applied</returns>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
