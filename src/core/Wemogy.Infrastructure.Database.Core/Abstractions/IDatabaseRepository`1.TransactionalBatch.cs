using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Starts a transactional batch against a single logical partition.
    /// </summary>
    /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
    /// <returns>An empty batch to add operations to</returns>
    IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(PartitionKeyValue partitionKey);
}
