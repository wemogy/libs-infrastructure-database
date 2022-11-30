using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public async Task DeleteAsync(string id)
    {
        if (!SoftDeleteState.IsEnabled)
        {
            var isDeleted = await _database.DeleteAsync(x => id == x.Id.ToString());
            if (isDeleted == 1)
            {
                return;
            }

            throw DatabaseError.EntityNotFound(id);
        }

        await UpdateAsync(
            id,
            SoftDelete);
    }

    public async Task DeleteAsync(string id, string partitionKey)
    {
        if (!SoftDeleteState.IsEnabled)
        {
            await _database.DeleteAsync(
                id,
                partitionKey);
            return;
        }

        await UpdateAsync(
            id,
            partitionKey,
            SoftDelete);
    }

    public async Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        if (!SoftDeleteState.IsEnabled)
        {
            return await _database.DeleteAsync(predicate);
        }

        var entities = await QueryAsync(predicate);
        var tasks = new List<Task>();
        foreach (var entity in entities)
        {
            SoftDelete(entity);
            tasks.Add(_database.ReplaceAsync(entity));
        }

        await Task.WhenAll(tasks);
        return entities.Count;
    }
}
