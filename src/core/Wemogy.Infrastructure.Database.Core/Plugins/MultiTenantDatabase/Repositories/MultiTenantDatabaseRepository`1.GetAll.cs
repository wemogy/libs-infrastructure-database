using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await _databaseRepository.QueryAsync(
            GetPartitionKeyPrefixCondition(),
            cancellationToken);

        foreach (var entity in entities)
        {
            RemovePartitionKeyPrefix(entity);
        }

        return entities;
    }
}
