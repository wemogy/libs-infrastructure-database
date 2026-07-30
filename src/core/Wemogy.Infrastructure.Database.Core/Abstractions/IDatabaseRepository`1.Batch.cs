namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     Defines batch-building methods on the repository. Use <see cref="CreateBatch"/> to obtain a
///     context, then add operations from any number of repositories before calling
///     <see cref="IBatchContext.ExecuteAsync"/>. All repositories involved must share the same
///     underlying database container (Cosmos constraint).
/// </summary>
public partial interface IDatabaseRepository<TEntity>
{
    /// <inheritdoc cref="IDatabaseClient{TEntity}.CreateBatch"/>
    IBatchContext CreateBatch(string partitionKey);

    /// <summary>Returns a batch operation that creates <paramref name="entity"/>.</summary>
    IBatchOperation ForBatchCreate(TEntity entity);

    /// <summary>Returns a batch operation that replaces <paramref name="entity"/>.</summary>
    IBatchOperation ForBatchReplace(TEntity entity);

    /// <summary>Returns a batch operation that deletes the entity with the given id.</summary>
    IBatchOperation ForBatchDelete(string id, string partitionKey);
}
