using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> CreateAsync(TEntity entity)
    {
        AddPartitionKeyPrefix(entity);

        await _databaseRepository.CreateAsync(entity);

        RemovePartiotionKeyPrefix(entity);
        return entity;
    }
}
