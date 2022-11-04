using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> CreateAsync(TEntity entity)
    {
        var partitionKeyValue = (string)_partitionKeyProperty.GetValue(entity);
        SetPartitionKeyValueInEntity(
            entity,
            partitionKeyValue);

        await _databaseRepository.CreateAsync(entity);

        RevertPartitionKeyValueInEntity(
            entity,
            partitionKeyValue);
        return entity;
    }
}
