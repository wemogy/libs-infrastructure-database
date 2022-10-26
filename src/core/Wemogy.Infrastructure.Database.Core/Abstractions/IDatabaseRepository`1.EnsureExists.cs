using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    Task EnsureExistsAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    Task EnsureExistsAsync(string id, CancellationToken cancellationToken = default);

    Task EnsureExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}
