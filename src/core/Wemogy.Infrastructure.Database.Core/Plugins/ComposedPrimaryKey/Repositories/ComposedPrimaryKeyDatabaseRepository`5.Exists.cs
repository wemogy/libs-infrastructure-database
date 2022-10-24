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
    public Task<bool> ExistsAsync(TId id, TPartitionKey partitionKey, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.ExistsAsync(
            BuildComposedPrimaryKey(id),
            partitionKey,
            cancellationToken);
    }

    public Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.ExistsAsync(
            BuildComposedPrimaryKey(id),
            cancellationToken);
    }

    public Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task EnsureExistAsync(TId id, TPartitionKey partitionKey, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.ExistsAsync(
            BuildComposedPrimaryKey(id),
            partitionKey,
            cancellationToken);
    }

    public Task EnsureExistAsync(TId id, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.ExistsAsync(
            BuildComposedPrimaryKey(id),
            cancellationToken);
    }

    public Task EnsureExistAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
