using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task DeleteAsync(string id)
    {
        try
        {
            await _databaseRepository.DeleteAsync(IdAndPartitionKeyPrefixedPredicate(id));
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }

    public async Task DeleteAsync(string id, string partitionKey)
    {
        try
        {
            await _databaseRepository.DeleteAsync(
                id,
                BuildComposedPartitionKey(partitionKey));
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }

    public async Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        try
        {
            predicate = BuildComposedPartitionKeyPredicate(predicate);
            return await _databaseRepository.DeleteAsync(predicate);
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }
}
