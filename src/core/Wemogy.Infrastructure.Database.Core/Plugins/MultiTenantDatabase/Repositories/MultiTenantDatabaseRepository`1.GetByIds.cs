using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<List<TEntity>> GetByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        Expression<Func<TEntity, bool>> idIsContainedPredicate = x => ids.Contains(x.Id);

        var entities = await _databaseRepository.QueryAsync(
            GetPartitionKeyPrefixCondition().And(idIsContainedPredicate),
            cancellationToken);

        foreach (var entity in entities)
        {
            RemovePartitionKeyPrefix(entity);
        }

        return entities;
    }
}
