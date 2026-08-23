using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     A set of write operations against a single logical partition that is applied
///     atomically: either every operation succeeds, or none of them is applied.
///     <para>
///         A batch is single-use and not thread-safe: build it from one thread, execute it
///         once. Read filters, property filters and soft-delete are not applied to its
///         operations, the same way <c>CreateAsync</c> and <c>ReplaceAsync</c> pass straight
///         through to the provider.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The entity type every operation of the batch acts on</typeparam>
public interface IDatabaseTransactionalBatch<TEntity>
{
    /// <summary>
    ///     The number of operations added so far.
    /// </summary>
    int OperationCount { get; }

    /// <summary>
    ///     Creates a new entity. Fails the whole batch if an entity with the same id already
    ///     exists in the batch's partition.
    /// </summary>
    /// <param name="entity">The entity to create</param>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabaseTransactionalBatch<TEntity> Create(TEntity entity);

    /// <summary>
    ///     Replaces an existing entity. If the entity opts into optimistic concurrency via
    ///     <see cref="Attributes.ETagAttribute"/> and carries an eTag, it is sent as a
    ///     precondition and a mismatch fails the whole batch.
    /// </summary>
    /// <param name="entity">The updated entity which will replace the existing one</param>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabaseTransactionalBatch<TEntity> Replace(TEntity entity);

    /// <summary>
    ///     Inserts or updates an entity. Carries no precondition, mirroring <c>UpsertAsync</c>.
    /// </summary>
    /// <param name="entity">The entity to insert or update</param>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabaseTransactionalBatch<TEntity> Upsert(TEntity entity);

    /// <summary>
    ///     Deletes the entity with the given id from the batch's partition. This is a hard
    ///     delete, consistent with <c>DeleteAsync</c>; a soft delete is a <see cref="Replace"/>
    ///     with the flag set.
    /// </summary>
    /// <param name="id">The id of the entity to delete</param>
    /// <returns>The same batch, so calls can be chained</returns>
    IDatabaseTransactionalBatch<TEntity> Delete(string id);

    /// <summary>
    ///     Executes every operation atomically. A batch with no operations completes without
    ///     touching the database.
    ///     <para>
    ///         No entities are returned: a per-item result would need an index-aligned type
    ///         that <see cref="Delete"/> cannot fill, and skipping the write response keeps the
    ///         request charge down. A caller that needs the post-write state, e.g. the new eTag,
    ///         re-reads the entity.
    ///     </para>
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the execution</param>
    /// <returns>A task that completes when every operation has been applied</returns>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
