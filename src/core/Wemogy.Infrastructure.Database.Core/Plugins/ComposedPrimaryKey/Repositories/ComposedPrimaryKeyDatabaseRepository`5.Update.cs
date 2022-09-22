using System;
using System.Threading.Tasks;
using Mapster;
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
    public Task<TEntity> UpdateAsync(TId id, TPartitionKey partitionKey, Action<TEntity> updateAction)
    {
        return UpdateAsync(
            id,
            partitionKey,
            entity =>
            {
                updateAction(entity);
                return Task.CompletedTask;
            });
    }

    public Task<TEntity> UpdateAsync(TId id, Action<TEntity> updateAction)
    {
        return UpdateAsync(
            id,
            entity =>
            {
                updateAction(entity);
                return Task.CompletedTask;
            });
    }

    public async Task<TEntity> UpdateAsync(TId id, TPartitionKey partitionKey, Func<TEntity, Task> updateAction)
    {
        var updatedInternalEntity = await _databaseRepository.UpdateAsync(
            BuildComposedPrimaryKey(id),
            partitionKey,
            async internalEntity =>
            {
                var entity = AdaptToEntity(internalEntity);
                await updateAction(entity);
                entity.Adapt(internalEntity);
                internalEntity.Id = BuildComposedPrimaryKey(id);
            });

        var entity = AdaptToEntity(updatedInternalEntity);

        return entity;
    }

    public async Task<TEntity> UpdateAsync(TId id, Func<TEntity, Task> updateAction)
    {
        var updatedInternalEntity = await _databaseRepository.UpdateAsync(
            BuildComposedPrimaryKey(id),
            async internalEntity =>
            {
                var entity = AdaptToEntity(internalEntity);
                await updateAction(entity);
            });

        var entity = AdaptToEntity(updatedInternalEntity);

        return entity;
    }
}
