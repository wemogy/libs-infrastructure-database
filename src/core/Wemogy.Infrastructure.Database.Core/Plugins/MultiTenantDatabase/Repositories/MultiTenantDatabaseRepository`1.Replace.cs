using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<TEntity> ReplaceAsync(TEntity entity)
    {
        throw new NotImplementedException();
    }
}
