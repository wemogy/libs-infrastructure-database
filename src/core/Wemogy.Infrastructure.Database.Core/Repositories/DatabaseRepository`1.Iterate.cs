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
        if (SoftDeleteState.IsEnabled)
        {
            predicate = predicate.And(_softDeleteFilterExpression);
        }

        callback = PropertyFilters.Wrap(callback);

        return _database.IterateAsync(
            predicate,
            callback,
            cancellationToken);
    }

    public Task IterateAsync(
        QueryParameters queryParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        Expression<Func<TEntity, bool>>? predicate = null;

        if (SoftDeleteState.IsEnabled)
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
