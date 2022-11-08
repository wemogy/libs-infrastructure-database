using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> UpdateAsync(string id, string partitionKey, Action<TEntity> updateAction)
    {
        var entity = await GetAsync(
            id,
            partitionKey);
        updateAction(entity);
        var updatedEntity = await _databaseRepository.ReplaceAsync(entity);
        return updatedEntity;
    }

    public async Task<TEntity> UpdateAsync(string id, Action<TEntity> updateAction)
    {
        var entity = await GetAsync(id);
        updateAction(entity);
        var updatedEntity = await _databaseRepository.ReplaceAsync(entity);
        return updatedEntity;
    }

    public async Task<TEntity> UpdateAsync(string id, string partitionKey, Func<TEntity, Task> updateAction)
    {
        var entity = await GetAsync(
            id,
            partitionKey);
        await updateAction(entity);
        var updatedEntity = await _databaseRepository.ReplaceAsync(entity);
        return updatedEntity;
    }

    public async Task<TEntity> UpdateAsync(string id, Func<TEntity, Task> updateAction)
    {
        var entity = await GetAsync(id);
        await updateAction(entity);
        var updatedEntity = await _databaseRepository.ReplaceAsync(entity);
        return updatedEntity;
    }
}
