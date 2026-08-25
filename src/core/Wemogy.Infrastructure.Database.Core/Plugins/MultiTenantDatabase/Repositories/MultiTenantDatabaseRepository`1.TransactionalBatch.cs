using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(PartitionKeyValue partitionKey)
    {
        return new MultiTenantTransactionalBatch<TEntity>(
            _databaseRepository.CreateTransactionalBatch(BuildComposedPartitionKey(partitionKey)),
            AddPartitionKeyPrefix,
            ComposeConditionPredicate,
            CleanupException);
    }
}
