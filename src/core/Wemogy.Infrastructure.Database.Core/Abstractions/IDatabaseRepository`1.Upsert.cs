using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
/// Defines methods for upserting entities in a database repository.
/// </summary>
public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Upsert an entity in the database.
    /// </summary>
    /// <param name="entity">The entity to upsert</param>
    /// <returns>The upserted entity as persisted</returns>
    Task<TEntity> UpsertAsync(TEntity entity);

    /// <summary>
    ///     Upsert an entity in the database, using the specified partition key.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="partitionKey">The partition key to use for the upsert operation.</param>
    /// <returns>The upserted entity as persisted</returns>
    Task<TEntity> UpsertAsync(TEntity entity, string partitionKey);
}
