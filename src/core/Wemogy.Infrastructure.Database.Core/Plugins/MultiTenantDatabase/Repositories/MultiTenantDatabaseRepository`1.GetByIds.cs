using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<List<TEntity>> GetByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
