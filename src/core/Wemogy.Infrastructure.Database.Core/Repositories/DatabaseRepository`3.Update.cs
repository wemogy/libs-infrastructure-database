using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity, TPartitionKey, TId>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
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
        var entity = await GetAsync(
            id,
            partitionKey);
        await updateAction(entity);
        var updatedEntity = await _database.ReplaceAsync(entity);
        return updatedEntity;
    }

    public async Task<TEntity> UpdateAsync(TId id, Func<TEntity, Task> updateAction)
    {
        var entity = await GetAsync(id);
        await updateAction(entity);
        var updatedEntity = await _database.ReplaceAsync(entity);
        return updatedEntity;
    }
}
