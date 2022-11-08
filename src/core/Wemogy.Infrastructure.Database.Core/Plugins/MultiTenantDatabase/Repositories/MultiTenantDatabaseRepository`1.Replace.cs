using System.Threading.Tasks;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> ReplaceAsync(TEntity entity)
    {
        var entityToUpdate = entity.Clone();
        AddPartitionKeyPrefix(entityToUpdate);
        var updated = await _databaseRepository.ReplaceAsync(entityToUpdate);
        RemovePartitionKeyPrefix(updated);
        return updated;
    }
}
