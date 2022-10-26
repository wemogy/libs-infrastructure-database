using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity> : IDatabaseRepository
    where TEntity : IEntityBase
{
    Task<TEntity> ReplaceAsync(TEntity entity);
}
