using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> ReplaceAsync(TEntity entity)
    {
        var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

        try
        {
            // see CreateAsync: the provider's entity carries the new eTag, the caller's does not
            var replacedEntity = await _databaseRepository.ReplaceAsync(entity);

            RemovePartitionKeyPrefix(replacedEntity);

            return replacedEntity;
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
