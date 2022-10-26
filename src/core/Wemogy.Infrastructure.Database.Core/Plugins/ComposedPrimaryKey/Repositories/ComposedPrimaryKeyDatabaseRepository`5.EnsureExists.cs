using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

public partial class ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity, TComposedPrimaryKeyBuilder>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
    where TInternalEntity : IEntityBase<string>
    where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<TId>
{
    public Task EnsureExistsAsync(TId id, TPartitionKey partitionKey, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.EnsureExistsAsync(
            BuildComposedPrimaryKey(id),
            partitionKey,
            cancellationToken);
    }

    public Task EnsureExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.EnsureExistsAsync(
            BuildComposedPrimaryKey(id),
            cancellationToken);
    }

    public Task EnsureExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
