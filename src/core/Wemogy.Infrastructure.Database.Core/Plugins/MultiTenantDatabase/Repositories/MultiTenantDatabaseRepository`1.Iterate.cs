using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        return IterateAsync(
            predicate,
            null,
            null,
            callback,
            cancellationToken);
    }

    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity> sortingParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        return IterateAsync(
            predicate,
            sortingParameters,
            null,
            callback,
            cancellationToken);
    }

    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        PaginationParameters paginationParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        return IterateAsync(
            predicate,
            null,
            paginationParameters,
            callback,
            cancellationToken);
    }

    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity>? sortingParameters,
        PaginationParameters? paginationParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        async Task UpdatedCallback(TEntity entity)
        {
            await callback(entity);
            RemovePartitionKeyPrefix(entity);
        }

        predicate = BuildComposedPartitionKeyPredicate(predicate);
        predicate = predicate.And(PartitionKeyPredicate);

        if (paginationParameters == null || sortingParameters == null)
        {
            if (sortingParameters != null)
            {
                return _databaseRepository.IterateAsync(
                    predicate,
                    sortingParameters,
                    UpdatedCallback,
                    cancellationToken);
            }

            if (paginationParameters != null)
            {
                return _databaseRepository.IterateAsync(
                    predicate,
                    paginationParameters,
                    UpdatedCallback,
                    cancellationToken);
            }

            return _databaseRepository.IterateAsync(
                predicate,
                UpdatedCallback,
                cancellationToken);
        }

        return _databaseRepository.IterateAsync(
            predicate,
            sortingParameters,
            paginationParameters,
            UpdatedCallback,
            cancellationToken);
    }

    public Task IterateAsync(
        QueryParameters queryParameters,
        Func<TEntity, Task> callback,
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

    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        Task UpdatedCallback(TEntity entity)
        {
            callback(entity);
            RemovePartitionKeyPrefix(entity);
            return Task.CompletedTask;
        }

        predicate = BuildComposedPartitionKeyPredicate(predicate);

        return _databaseRepository.IterateAsync(
            predicate.And(
                PartitionKeyPredicate),
            UpdatedCallback,
            cancellationToken);
    }

    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        PaginationParameters paginationParameters,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        return IterateAsync(
            predicate,
            null,
            paginationParameters,
            callback,
            cancellationToken);
    }

    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity> sortingParameters,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        return IterateAsync(
            predicate,
            sortingParameters,
            null,
            callback,
            cancellationToken);
    }

    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity>? sortingParameters,
        PaginationParameters? paginationParameters,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        return IterateAsync(
            predicate,
            sortingParameters,
            paginationParameters,
            entity =>
            {
                callback(entity);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    public Task IterateAsync(
        QueryParameters queryParameters,
        Action<TEntity> callback,
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
