using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> CreateAsync(TEntity entity)
    {
        try
        {
            var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

            // the entity the provider returns carries the values it assigned itself, e.g. the
            // eTag. Returning the caller's instance instead would drop them, which switches
            // optimistic concurrency off for multi-tenant repositories.
            var createdEntity = await _databaseRepository.CreateAsync(entity);

            removePartitionKeyPrefixAction();
            RemovePartitionKeyPrefix(createdEntity);

            return createdEntity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }
}
