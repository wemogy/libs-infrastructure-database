using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

/// <summary>
/// Repository for handling multi-tenant database operations for <typeparamref name="TEntity"/>.
/// </summary>
public partial class MultiTenantDatabaseRepository<TEntity>
{
    /// <summary>
    /// Inserts or updates the specified entity in the database, scoped to the current tenant.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <returns>The upserted entity.</returns>
    public async Task<TEntity> UpsertAsync(TEntity entity)
    {
        try
        {
            var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

            // the entity the provider returns carries the values it assigned itself, e.g. the eTag
            var upsertedEntity = await _databaseRepository.UpsertAsync(entity);

            removePartitionKeyPrefixAction();
            RemovePartitionKeyPrefix(upsertedEntity);

            return upsertedEntity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }

    /// <summary>
    /// Inserts or updates the specified entity in the database using the provided partition key,
    /// scoped to the current tenant.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="partitionKey">The partition key to use for the operation.</param>
    /// <returns>The upserted entity.</returns>
    public async Task<TEntity> UpsertAsync(TEntity entity, string partitionKey)
    {
        try
        {
            // the entity's own partition key has to be prefixed as well, otherwise the stored
            // document would disagree with the partition it was written to
            var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

            var upsertedEntity = await _databaseRepository.UpsertAsync(
                entity,
                BuildComposedPartitionKey(partitionKey));

            removePartitionKeyPrefixAction();
            RemovePartitionKeyPrefix(upsertedEntity);

            return upsertedEntity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }
}
