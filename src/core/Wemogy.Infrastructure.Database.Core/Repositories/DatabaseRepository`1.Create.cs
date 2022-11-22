using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    public Task<TEntity> CreateAsync(TEntity entity)
    {
        return _database.CreateAsync(entity);
    }
}
