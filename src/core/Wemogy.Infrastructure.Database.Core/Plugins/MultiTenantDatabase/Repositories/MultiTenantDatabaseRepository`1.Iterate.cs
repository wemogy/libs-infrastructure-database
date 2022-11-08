using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        // if (SoftDelete.IsEnabled)
        // {
        //     predicate = predicate.And(_softDeleteFilterExpression);
        // }
        //
        // callback = PropertyFilters.Wrap(callback);
        //
        // return _databaseRepository.IterateAsync(
        //     predicate,
        //     callback,
        //     cancellationToken);

        throw new NotImplementedException();

        // Task UpdatedAction(TEntity entity)
        // {
        //     RemovePartitionKeyPrefix(entity);
        //     updateAction(entity);
        //     AddPartitionKeyPrefix(entity);
        //     return Task.CompletedTask;
        // }
        //
        // var updatedEntity = await _databaseRepository.UpdateAsync(
        //     id,
        //     BuildComposedPartitionKey(partitionKey),
        //     UpdatedAction);
        //
        // RemovePartitionKeyPrefix(updatedEntity);
        // return updatedEntity;
    }

    public Task IterateAsync(QueryParameters queryParameters, Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        async Task UpdatedCallback(TEntity entity)
        {
            await callback(entity);
            RemovePartitionKeyPrefix(entity);
        }

        return _databaseRepository.IterateAsync(
            queryParameters,
            UpdatedCallback,
            cancellationToken);
    }

    public Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
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

        return _databaseRepository.IterateAsync(
            queryParameters,
            UpdatedCallback,
            cancellationToken);
    }
}
