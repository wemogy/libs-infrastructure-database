using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public IBatchContext CreateBatch(string partitionKey)
        => _database.CreateBatch(partitionKey);

    public IBatchOperation ForBatchCreate(TEntity entity)
        => _database.CreateBatchOperationForCreate(entity);

    public IBatchOperation ForBatchReplace(TEntity entity)
        => _database.CreateBatchOperationForReplace(entity);

    public IBatchOperation ForBatchDelete(string id, string partitionKey)
        => _database.CreateBatchOperationForDelete(id, partitionKey);
}
