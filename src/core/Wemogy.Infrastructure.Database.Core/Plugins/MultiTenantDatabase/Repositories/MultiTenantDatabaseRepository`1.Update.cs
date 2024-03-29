using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<TEntity> UpdateAsync(string id, string partitionKey, Action<TEntity> updateAction)
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

    public async Task<TEntity> UpdateAsync(string id, string partitionKey, Func<TEntity, Task> updateAction)
    {
        var updated = await _databaseRepository.UpdateAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            entity =>
            {
                RemovePartitionKeyPrefix(entity);
                updateAction(entity);
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
