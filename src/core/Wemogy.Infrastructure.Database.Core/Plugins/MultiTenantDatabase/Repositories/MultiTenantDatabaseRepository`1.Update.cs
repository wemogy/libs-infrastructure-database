using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<TEntity> UpdateAsync(string id, string partitionKey, Action<TEntity> updateAction)
    {
        throw new NotImplementedException();
    }

    public Task<TEntity> UpdateAsync(string id, Action<TEntity> updateAction)
    {
        throw new NotImplementedException();
    }

    public Task<TEntity> UpdateAsync(string id, string partitionKey, Func<TEntity, Task> updateAction)
    {
        throw new NotImplementedException();
    }

    public Task<TEntity> UpdateAsync(string id, Func<TEntity, Task> updateAction)
    {
        throw new NotImplementedException();
    }
}
