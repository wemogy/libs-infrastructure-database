using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> CreateAsync(TEntity entity)
    {
        var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

        try
        {
            // the entity the provider returns carries the values it assigned itself, e.g. the
            // eTag. Returning the caller's instance instead would drop them, which switches
            // optimistic concurrency off for multi-tenant repositories.
            var createdEntity = await _databaseRepository.CreateAsync(entity);

            RemovePartitionKeyPrefix(createdEntity);

            return createdEntity;
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
