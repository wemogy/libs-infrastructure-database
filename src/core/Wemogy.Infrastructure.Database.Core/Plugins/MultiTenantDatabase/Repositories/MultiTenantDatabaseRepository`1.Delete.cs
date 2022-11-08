using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task DeleteAsync(string id)
    {
        throw new NotImplementedException();
    }

    public Task DeleteAsync(string id, string partitionKey)
    {
        return _databaseRepository.DeleteAsync(
            id,
            BuildComposedPartitionKey(partitionKey));
    }

    public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        // TODO: filter by composite partition key
        return _databaseRepository.DeleteAsync(predicate);
    }
}
