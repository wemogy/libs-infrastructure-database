using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<TEntity> UpdateAsync(string id, PartitionKeyValue partitionKey, Action<TEntity> updateAction)
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

    public Task<TEntity> UpdateAsync(string id, Action<TEntity> updateAction)
    {
        return UpdateAsync(
            id,
            entity =>
            {
                updateAction(entity);
                return Task.CompletedTask;
            });
    }

    public async Task<TEntity> UpdateAsync(string id, PartitionKeyValue partitionKey, Func<TEntity, Task> updateAction)
    {
        // the update action is awaited, and the lambda is async so that it binds to the
        // Func<TEntity, Task> overload: an action that actually awaits used to have its
        // continuation run after the entity had already been written, which dropped the mutation
        // and raced the write
        var updated = await _databaseRepository.UpdateAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            async entity =>
            {
                RemovePartitionKeyPrefix(entity);
                await updateAction(entity);
                AddPartitionKeyPrefix(entity);
            });

        ReplacePartitionKey(
            updated,
            partitionKey);
        return updated;
    }

    public async Task<TEntity> UpdateAsync(string id, Func<TEntity, Task> updateAction)
    {
        var entity = await GetAsync(id);
        await updateAction(entity);
        var updatedEntity = await ReplaceAsync(entity);
        return updatedEntity;
    }
}
