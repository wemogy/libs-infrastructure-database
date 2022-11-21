using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> CreateAsync(TEntity entity)
    {
        try
        {
            var removePartitionKeyPrefixAction = AddPartitionKeyPrefix(entity);

            await _databaseRepository.CreateAsync(entity);

            removePartitionKeyPrefixAction();

            return entity;
        }
        catch (Exception e)
        {
            CleanupException(e);
            throw;
        }
    }
}
