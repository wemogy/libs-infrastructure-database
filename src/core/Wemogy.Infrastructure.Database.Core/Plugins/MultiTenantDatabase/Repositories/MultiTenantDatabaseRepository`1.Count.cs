using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<long> CountAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            predicate = BuildComposedPartitionKeyPredicate(predicate);
            predicate = predicate.And(PartitionKeyPredicate);
            return await _databaseRepository.CountAsync(
                predicate,
                cancellationToken);
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }
}
