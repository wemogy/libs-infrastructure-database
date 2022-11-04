using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<TEntity> GetAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _databaseRepository.GetAsync(
            id,
            BuildComposedPartitionKey(partitionKey),
            cancellationToken);

        RemovePartiotionKeyPrefix(entity);
        return entity;
    }

    // public Task<TEntity> GetAsync(
    //     string id,
    //     CancellationToken cancellationToken = default)
    // {
    //     var partitionKeyPrefix = GetPartitionKeyPrefix();
    //
    //     // ToDo: Create lambda expression by using the PartitionKeyProperty
    //     // ToDo: We need to do: x.Id == id && partitionKeyProperty(x).StartsWith(partitionKeyPrefix)
    //     // Tipp: Take a look at DatabaseRepository line 65-85
    //     return _databaseRepository.GetAsync(
    //         x => x.Id == id,
    //         cancellationToken);
    // }
}
