using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : IEntityBase
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
        var entity = await GetAsync(
            id,
            partitionKey);
        await updateAction(entity);
        var updatedEntity = await _database.ReplaceAsync(entity);
        return updatedEntity;
    }

    public async Task<TEntity> UpdateAsync(string id, Func<TEntity, Task> updateAction)
    {
        var entity = await GetAsync(id);
        await updateAction(entity);
        var updatedEntity = await _database.ReplaceAsync(entity);
        return updatedEntity;
    }
}
