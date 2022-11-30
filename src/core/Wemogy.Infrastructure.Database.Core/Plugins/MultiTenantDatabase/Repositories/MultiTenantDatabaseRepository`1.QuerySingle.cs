using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> QuerySingleAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            predicate = BuildComposedPartitionKeyPredicate(predicate);

            var entity = await _databaseRepository.QuerySingleAsync(
                predicate,
                cancellationToken);

            RemovePartitionKeyPrefix(entity);

            return entity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }
}
