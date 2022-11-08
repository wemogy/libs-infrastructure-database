using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> UpdateAsync(string id, string partitionKey, Action<TEntity> updateAction)
    {
        Task UpdatedAction(TEntity entity)
        {
            RemovePartitionKeyPrefix(entity);
            updateAction(entity);
            AddPartitionKeyPrefix(entity);
            return Task.CompletedTask;
        }

        var updatedEntity = await _databaseRepository.UpdateAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            UpdatedAction);

        RemovePartitionKeyPrefix(updatedEntity);
        return updatedEntity;
    }

    public Task<TEntity> UpdateAsync(string id, Action<TEntity> updateAction)
    {
        throw new NotImplementedException();
    }

    public async Task<TEntity> UpdateAsync(string id, string partitionKey, Func<TEntity, Task> updateAction)
    {
        async Task UpdatedAction(TEntity entity)
        {
            RemovePartitionKeyPrefix(entity);
            await updateAction(entity);
            AddPartitionKeyPrefix(entity);
        }

        var updatedEntity = await _databaseRepository.UpdateAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            UpdatedAction);

        RemovePartitionKeyPrefix(updatedEntity);
        return updatedEntity;
    }

    public async Task<TEntity> UpdateAsync(string id, Func<TEntity, Task> updateAction)
    {
        throw new NotImplementedException();
    }
}
