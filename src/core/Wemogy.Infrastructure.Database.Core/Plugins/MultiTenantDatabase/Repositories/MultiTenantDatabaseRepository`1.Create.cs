using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<TEntity> CreateAsync(TEntity entity)
    {
        return _databaseRepository.CreateAsync(entity);
    }
}
