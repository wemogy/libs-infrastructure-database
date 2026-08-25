using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IDatabaseClient<TEntity> : IDatabaseClient
    where TEntity : class
{
    Task<TEntity> GetAsync(string id, string partitionKey, CancellationToken cancellationToken);

    Task IterateAsync(
        QueryParameters queryParameters,
        Expression<Func<TEntity, bool>>? generalFilterPredicate,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Iterates over all items which matches the predicate.
    /// </summary>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Sorting<TEntity>? sorting,
        Pagination? pagination,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Counts all items which matches the predicate.
    /// </summary>
    Task<long> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Creates a new entity.
    /// </summary>
    Task<TEntity> CreateAsync(TEntity entity);

    /// <summary>
    ///     Replaces an existing entity in the database.
    /// </summary>
    Task<TEntity> ReplaceAsync(TEntity entity);

    Task DeleteAsync(string id, string partitionKey);

    Task DeleteAsync(Expression<Func<TEntity, bool>> predicate);

    Task<TEntity> UpsertAsync(TEntity entity);

    Task<TEntity> UpsertAsync(TEntity entity, string partitionKey);

    /// <summary>
    ///     Starts a transactional batch against a single logical partition.
    /// </summary>
    IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(string partitionKey);

    /// <summary>
    ///     Applies a partial update to a single document, optionally only if the given condition
    ///     holds. The condition and the operations are applied as one atomic operation.
    /// </summary>
    Task<TEntity> PatchAsync(
        string id,
        string partitionKey,
        Action<IPatchOperations<TEntity>> operations,
        Expression<Func<TEntity, bool>>? condition,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Creates a stopped processor reading the latest version change feed of this collection.
    /// </summary>
    IChangeFeedProcessor CreateChangeFeedProcessor(
        string processorName,
        ChangeFeedHandler<TEntity> onChanges,
        ChangeFeedProcessorOptions? options);

    /// <summary>
    ///     Creates a stopped processor reading the all-versions-and-deletes change feed of this
    ///     collection.
    /// </summary>
    IChangeFeedProcessor CreateAllVersionsAndDeletesChangeFeedProcessor(
        string processorName,
        AllVersionsAndDeletesChangeFeedHandler<TEntity> onChanges,
        ChangeFeedProcessorOptions? options);
}
