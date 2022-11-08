using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _databaseRepository.QueryAsync(
            GetPartitionKeyPrefixCondition(),
            cancellationToken);
    }
}
