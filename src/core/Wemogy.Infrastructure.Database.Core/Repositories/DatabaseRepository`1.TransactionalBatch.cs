using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(PartitionKeyValue partitionKey)
    {
        return _database.CreateTransactionalBatch(partitionKey);
    }
}
