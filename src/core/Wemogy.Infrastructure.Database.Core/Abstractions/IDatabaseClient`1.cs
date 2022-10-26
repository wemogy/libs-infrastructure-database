using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IDatabaseClient<TEntity> : IDatabaseClient
    where TEntity : IEntityBase
{
    Task<TEntity> GetAsync(string id, string partitionKey, CancellationToken cancellationToken);

    Task IterateAsync(QueryParameters queryParameters, Expression<Func<TEntity, bool>>? generalFilterPredicate,
        Func<TEntity, Task> callback, CancellationToken cancellationToken);

    /// <summary>
    ///     Iterates over all items which matches the predicate.
    /// </summary>
    Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Func<TEntity, Task> callback,
        CancellationToken cancellationToken);

    /// <summary>
    ///     Creates a new entity.
    /// </summary>
    Task<TEntity> CreateAsync(TEntity entity);

    /// <summary>
    ///     Replaces an existing entity in the database.
    /// </summary>
    Task<TEntity> ReplaceAsync(TEntity entity);

    Task DeleteAsync(string id, string partitionKey);

    Task DeleteAsync(Expression<Func<TEntity, bool>> predicate);
}
