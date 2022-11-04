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
        throw new NotImplementedException();
    }

    public Task IterateAsync(QueryParameters queryParameters, Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task IterateAsync(QueryParameters queryParameters, Action<TEntity> callback,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
