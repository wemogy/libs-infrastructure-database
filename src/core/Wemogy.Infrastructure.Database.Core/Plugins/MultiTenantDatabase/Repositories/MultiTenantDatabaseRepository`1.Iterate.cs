using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        async Task UpdatedCallback(TEntity entity)
        {
            await callback(entity);
            RemovePartitionKeyPrefix(entity);
        }

        return _databaseRepository.IterateAsync(
            predicate.And(
                GetPartitionKeyPrefixCondition()),
            UpdatedCallback,
            cancellationToken);
    }

    public Task IterateAsync(QueryParameters queryParameters, Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        async Task UpdatedCallback(TEntity entity)
        {
            await callback(entity);
            RemovePartitionKeyPrefix(entity);
        }

        var queryParametersInternal = GetQueryParametersWithPartitionKeyFilter(queryParameters);

        return _databaseRepository.IterateAsync(
            queryParametersInternal,
            UpdatedCallback,
            cancellationToken);
    }

    public Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        Task UpdatedCallback(TEntity entity)
        {
            callback(entity);
            RemovePartitionKeyPrefix(entity);
            return Task.CompletedTask;
        }

        return _databaseRepository.IterateAsync(
            predicate.And(
                GetPartitionKeyPrefixCondition()),
            UpdatedCallback,
            cancellationToken);
    }

    public Task IterateAsync(QueryParameters queryParameters, Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        Task UpdatedCallback(TEntity entity)
        {
            callback(entity);
            RemovePartitionKeyPrefix(entity);
            return Task.CompletedTask;
        }

        var queryParametersInternal = GetQueryParametersWithPartitionKeyFilter(queryParameters);

        return _databaseRepository.IterateAsync(
            queryParametersInternal,
            UpdatedCallback,
            cancellationToken);
    }
}
