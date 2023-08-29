using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
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
        Sorting<TEntity> sortingParameters,
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

    public async Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Sorting<TEntity>? sortingParameters,
        PaginationParameters? paginationParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        if (SoftDelete.IsEnabled)
        {
            predicate = predicate.And(_softDeleteFilterExpression);
        }

        var filter = await GetReadFilter();
        predicate = predicate.And(filter);

        callback = PropertyFilters.Wrap(callback);

        await _database.IterateAsync(
            predicate,
            sortingParameters,
            paginationParameters,
            callback,
            cancellationToken);
    }

    public Task IterateAsync(
        QueryParameters queryParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        Expression<Func<TEntity, bool>>? predicate = null;

        if (SoftDelete.IsEnabled)
        {
            predicate = _softDeleteFilterExpression;
        }

        callback = PropertyFilters.Wrap(callback);

        return _database.IterateAsync(
            queryParameters,
            predicate,
            callback,
            cancellationToken);
    }

    public Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> callback,
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
        Sorting<TEntity> sortingParameters,
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
        Sorting<TEntity>? sortingParameters,
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
        return IterateAsync(
            queryParameters,
            entity =>
            {
                callback(entity);
                return Task.CompletedTask;
            },
            cancellationToken);
    }
}
