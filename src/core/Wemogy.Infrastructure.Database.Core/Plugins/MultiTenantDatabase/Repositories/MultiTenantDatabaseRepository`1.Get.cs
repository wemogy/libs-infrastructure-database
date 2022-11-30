using System;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> GetAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _databaseRepository.GetAsync(
                id,
                BuildComposedPartitionKey(partitionKey),
                cancellationToken);

            ReplacePartitionKey(
                entity,
                partitionKey);

            return entity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }

    public async Task<TEntity> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            var entity = await _databaseRepository.QuerySingleAsync(
                IdAndPartitionKeyPrefixedPredicate(id),
                cancellationToken);

            RemovePartitionKeyPrefix(entity);

            return entity;
        }
        catch (NotFoundErrorException)
        {
            throw DatabaseError.EntityNotFound(id);
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }
}
