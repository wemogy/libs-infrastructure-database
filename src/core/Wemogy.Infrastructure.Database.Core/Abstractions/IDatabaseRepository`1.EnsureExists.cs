using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    Task EnsureExistsAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    Task EnsureExistsAsync(string id, CancellationToken cancellationToken = default);

    Task EnsureExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}
