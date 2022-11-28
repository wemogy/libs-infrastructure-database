using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public Task<TEntity> ReplaceAsync(TEntity entity)
    {
        return _database.ReplaceAsync(entity);
    }
}
