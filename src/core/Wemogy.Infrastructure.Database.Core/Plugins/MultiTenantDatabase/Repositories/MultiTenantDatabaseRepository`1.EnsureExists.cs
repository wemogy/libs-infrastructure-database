using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task EnsureExistsAsync(string id, string partitionKey, CancellationToken cancellationToken = default)
    {
        return GetAndWrapAroundNotFoundExceptionIfNotExists(
            id,
            partitionKey,
            () => _databaseRepository.EnsureExistsAsync(
                id,
                BuildComposedPartitionKey(partitionKey),
                cancellationToken));
    }

    public Task EnsureExistsAsync(string id, CancellationToken cancellationToken = default)
    {
        return GetAndWrapAroundNotFoundExceptionIfNotExists(
            id,
            null,
            () => _databaseRepository.EnsureExistsAsync(
                IdAndPartitionKeyPrefixedPredicate(id),
                cancellationToken));
    }

    public Task EnsureExistsAsync(Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return GetAndWrapAroundNotFoundExceptionIfNotExists(
            null,
            null,
            () => _databaseRepository.EnsureExistsAsync(
                PartitionKeyPredicate.And(predicate),
                cancellationToken));
    }
}
