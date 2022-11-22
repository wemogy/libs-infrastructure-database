using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task EnsureExistsAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        try
        {
            await _databaseRepository.EnsureExistsAsync(
                id,
                BuildComposedPartitionKey(partitionKey),
                cancellationToken);
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }

    public async Task EnsureExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await _databaseRepository.EnsureExistsAsync(
                IdAndPartitionKeyPrefixedPredicate(id),
                cancellationToken);
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }

    public async Task EnsureExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            predicate = BuildComposedPartitionKeyPredicate(predicate);
            await _databaseRepository.EnsureExistsAsync(
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
