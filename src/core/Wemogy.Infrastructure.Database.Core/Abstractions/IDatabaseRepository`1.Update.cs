using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    Task<TEntity> UpdateAsync(string id, string partitionKey, Action<TEntity> updateAction);

    Task<TEntity> UpdateAsync(string id, Action<TEntity> updateAction);

    Task<TEntity> UpdateAsync(string id, string partitionKey, Func<TEntity, Task> updateAction);

    Task<TEntity> UpdateAsync(string id, Func<TEntity, Task> updateAction);
}
