using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

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
        var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

        try
        {
            // the entity the provider returns carries the values it assigned itself, e.g. the eTag
            var upsertedEntity = await _databaseRepository.UpsertAsync(entity);

            RemovePartitionKeyPrefix(upsertedEntity);

            return upsertedEntity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
        finally
        {
            // rolled back in a finally: if the write throws, a caller that retries with the same
            // instance would prefix the already prefixed value and address a partition that no read
            // path composes, so the retried write would silently disappear
            removePartitionKeyPrefixAction();
        }
    }

    /// <summary>
    /// Inserts or updates the specified entity in the database using the provided partition key,
    /// scoped to the current tenant.
    /// </summary>
    /// <param name="entity">The entity to upsert.</param>
    /// <param name="partitionKey">The partition key to use for the operation.</param>
    /// <returns>The upserted entity.</returns>
    public async Task<TEntity> UpsertAsync(TEntity entity, PartitionKeyValue partitionKey)
    {
        // the entity's own partition key has to be prefixed as well, otherwise the stored
        // document would disagree with the partition it was written to
        var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

        try
        {
            var upsertedEntity = await _databaseRepository.UpsertAsync(
                entity,
                BuildComposedPartitionKey(partitionKey));

            RemovePartitionKeyPrefix(upsertedEntity);

            return upsertedEntity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
        finally
        {
            // rolled back in a finally: if the write throws, a caller that retries with the same
            // instance would prefix the already prefixed value and address a partition that no read
            // path composes, so the retried write would silently disappear
            removePartitionKeyPrefixAction();
        }
    }
}
