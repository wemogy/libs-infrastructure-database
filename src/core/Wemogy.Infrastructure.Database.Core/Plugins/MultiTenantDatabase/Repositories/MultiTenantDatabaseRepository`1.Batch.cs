using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public IBatchContext CreateBatch(string partitionKey)
    {
        return _databaseRepository.CreateBatch(BuildComposedPartitionKey(partitionKey));
    }

    public IBatchOperation ForBatchCreate(TEntity entity)
    {
        var clone = entity.Clone();
        AddPartitionKeyPrefix(clone);
        return _databaseRepository.ForBatchCreate(clone);
    }

    public IBatchOperation ForBatchReplace(TEntity entity)
    {
        var clone = entity.Clone();
        AddPartitionKeyPrefix(clone);
        return _databaseRepository.ForBatchReplace(clone);
    }

    public IBatchOperation ForBatchDelete(string id, string partitionKey)
    {
        return _databaseRepository.ForBatchDelete(id, BuildComposedPartitionKey(partitionKey));
    }
}
