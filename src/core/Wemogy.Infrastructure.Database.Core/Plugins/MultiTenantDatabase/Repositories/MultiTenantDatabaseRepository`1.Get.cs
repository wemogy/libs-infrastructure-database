using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<TEntity> GetAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        return _databaseRepository.GetAsync(
            id,
            partitionKey,
            cancellationToken);
        return _databaseRepository.GetAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            cancellationToken);
    }

    public Task<TEntity> GetAsync(
        string id,
        CancellationToken cancellationToken = default)
    {

        var partitionKeyPrefix = GetPartitionKeyPrefix();
        // ToDo: Create lamba expression by using the PartitionKeyProperty
        // ToDo: We need to do: x.Id == id && paritionKeyProperty(x).StartsWith(partitionKeyPrefix)
        // Tipp: Take a look at DatabaseRepository line 65-85
        return _databaseRepository.GetAsync(x => x.Id == id,
            cancellationToken);
    }
}
