using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<List<TEntity>> GetByIdsAsync(List<string> ids, CancellationToken cancellationToken = default)
    {
        // TODO: construct composite partition key predicate
        Expression<Func<TEntity, bool>> compositeTenantIdConditionPredicate = _ => true;

        Expression<Func<TEntity, bool>> idIsContainedPredicate = x => ids.Contains(x.Id);

        return _databaseRepository.QueryAsync(
            compositeTenantIdConditionPredicate.And(idIsContainedPredicate),
            cancellationToken);
    }
}
