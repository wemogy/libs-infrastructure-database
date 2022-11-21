using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default);

    Task IterateAsync(
        QueryParameters queryParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default);

    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default);

    Task IterateAsync(
        QueryParameters queryParameters,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default);
}
