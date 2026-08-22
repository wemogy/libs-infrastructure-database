using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> ReplaceAsync(TEntity entity)
    {
        var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

        // see CreateAsync: the provider's entity carries the new eTag, the caller's does not
        var replacedEntity = await _databaseRepository.ReplaceAsync(entity);

        removePartitionKeyPrefixAction();
        RemovePartitionKeyPrefix(replacedEntity);

        return replacedEntity;
    }
}
