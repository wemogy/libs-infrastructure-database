using System.Threading.Tasks;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> ReplaceAsync(TEntity entity)
    {
        var entityToUpdate = entity.Clone();
        var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entityToUpdate);
        var updated = await _databaseRepository.ReplaceAsync(entityToUpdate);
        removePartitionKeyPrefixAction();
        return updated;
    }
}
