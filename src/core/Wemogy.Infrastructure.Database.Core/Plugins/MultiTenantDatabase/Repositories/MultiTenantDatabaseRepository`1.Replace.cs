using System.Threading.Tasks;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> ReplaceAsync(TEntity entity)
    {
        var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);
        await _databaseRepository.ReplaceAsync(entity);
        removePartitionKeyPrefixAction();
        return entity;
    }
}
