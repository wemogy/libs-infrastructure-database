using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task DeleteAsync(string id)
    {
        return GetAndWrapAroundNotFoundExceptionIfNotExists(
            id,
            null,
            () => _databaseRepository.DeleteAsync(IdAndPartitionKeyPrefixedPredicate(id)));
    }

    public Task DeleteAsync(string id, string partitionKey)
    {
        return GetAndWrapAroundNotFoundExceptionIfNotExists(
            id,
            partitionKey,
            () => _databaseRepository.DeleteAsync(
                id,
                BuildComposedPartitionKey(partitionKey)));
    }

    public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return GetAndWrapAroundNotFoundExceptionIfNotExists(
            null,
            null,
            () => _databaseRepository.DeleteAsync(predicate.And(PartitionKeyPredicate)));
    }
}
