using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(string partitionKey)
    {
        return new MultiTenantTransactionalBatch<TEntity>(
            _databaseRepository.CreateTransactionalBatch(BuildComposedPartitionKey(partitionKey)),
            AddPartitionKeyPrefix,
            CleanupException);
    }
}
