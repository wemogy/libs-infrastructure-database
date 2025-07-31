using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

/// <summary>
/// Repository for handling multi-tenant database operations for <typeparamref name="TEntity"/>.
/// </summary>
public partial class MultiTenantDatabaseRepository<TEntity>
{
    /// <summary>
    /// Inserts or updates the specified entity in the database.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <returns>The upserted entity.</returns>
    public Task<TEntity> UpsertAsync(TEntity entity)
    {
        return _databaseRepository.UpsertAsync(entity);
    }

    /// <summary>
    /// Inserts or updates the specified entity in the database using the provided partition key.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="partitionKey">The partition key to use for the operation.</param>
    /// <returns>The upserted entity.</returns>
    public Task<TEntity> UpsertAsync(TEntity entity, string partitionKey)
    {
        return _databaseRepository.UpsertAsync(entity, partitionKey);
    }
}
