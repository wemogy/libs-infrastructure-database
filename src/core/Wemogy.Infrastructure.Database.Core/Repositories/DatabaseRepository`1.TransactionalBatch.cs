using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(string partitionKey)
    {
        return _database.CreateTransactionalBatch(partitionKey);
    }
}
