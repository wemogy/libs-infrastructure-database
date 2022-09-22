using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

public partial class ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity,
    TComposedPrimaryKeyBuilder>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
    where TInternalEntity : IEntityBase<string>
    where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<TId>
{
    public async Task<TEntity> GetAsync(
        TId id,
        TPartitionKey partitionKey,
        CancellationToken cancellationToken = default)
    {
        var internalEntity = await _databaseRepository.GetAsync(
            BuildComposedPrimaryKey(id),
            partitionKey,
            cancellationToken);

        var entity = AdaptToEntity(internalEntity);

        return entity;
    }

    public async Task<TEntity> GetAsync(TId id, CancellationToken cancellationToken = default)
    {
        var internalEntity = await _databaseRepository.GetAsync(
            BuildComposedPrimaryKey(id),
            cancellationToken);

        var entity = AdaptToEntity(internalEntity);

        return entity;
    }

    public Task<TEntity> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
