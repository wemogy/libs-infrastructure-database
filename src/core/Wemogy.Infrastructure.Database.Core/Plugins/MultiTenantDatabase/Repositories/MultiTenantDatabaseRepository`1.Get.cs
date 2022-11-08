using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> GetAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _databaseRepository.GetAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            cancellationToken);

        RemovePartitionKeyPrefix(entity);
        return entity;
    }

    public async Task<TEntity> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        Expression<Func<TEntity, bool>> idFilterPredicate = x => x.Id == id;

        var entity = await _databaseRepository.GetAsync(
            PartitionKeyPredicate.And(idFilterPredicate),
            cancellationToken);

        RemovePartitionKeyPrefix(entity);
        return entity;
    }

    public async Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entity = await _databaseRepository.GetAsync(
            PartitionKeyPredicate.And(predicate),
            cancellationToken);

        RemovePartitionKeyPrefix(entity);
        return entity;
    }
}
