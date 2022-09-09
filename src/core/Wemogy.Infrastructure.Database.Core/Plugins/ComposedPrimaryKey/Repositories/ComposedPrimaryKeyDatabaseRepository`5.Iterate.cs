using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

public partial class ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity, TComposedPrimaryKeyBuilder>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
    where TInternalEntity : IEntityBase<string>
    where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<TId>
{
    public Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Func<TEntity, Task> callback, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task IterateAsync(QueryParameters queryParameters, Func<TEntity, Task> callback, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Action<TEntity> callback, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task IterateAsync(QueryParameters queryParameters, Action<TEntity> callback, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
