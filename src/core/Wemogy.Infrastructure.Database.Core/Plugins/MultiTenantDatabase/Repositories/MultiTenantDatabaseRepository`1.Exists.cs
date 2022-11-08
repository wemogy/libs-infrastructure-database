using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<bool> ExistsAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        return _databaseRepository.ExistsAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            cancellationToken);
    }

    public async Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            await GetAsync(
                id,
                cancellationToken);
            return true;
        }
        catch (NotFoundErrorException)
        {
            return false;
        }
    }

    public async Task<bool> ExistsAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await GetAsync(
                predicate,
                cancellationToken);
            return true;
        }
        catch (NotFoundErrorException)
        {
            return false;
        }
    }
}
