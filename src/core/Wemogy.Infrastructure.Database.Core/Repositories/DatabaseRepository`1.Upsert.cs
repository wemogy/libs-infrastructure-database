using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

/// <summary>
/// Represents a repository for performing database operations on entities of type <typeparamref name="TEntity"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    /// <summary>
    /// Inserts or updates the specified entity in the database using the provided partition key.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="partitionKey">The partition key for the entity.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the upserted entity.</returns>
    public Task<TEntity> UpsertAsync(TEntity entity, string partitionKey)
    {
        return _database.UpsertAsync(
            entity,
            partitionKey);
    }

    /// <summary>
    /// Inserts or updates the specified entity in the database.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the upserted entity.</returns>
    public Task<TEntity> UpsertAsync(TEntity entity)
    {
        return _database.UpsertAsync(entity);
    }
}
