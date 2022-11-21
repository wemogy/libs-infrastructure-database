using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    Task<TEntity> CreateAsync(TEntity entity);
}
