using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Persist an entity in the repository.
    /// </summary>
    /// <param name="entity">The entity instance to persist.</param>
    /// <returns>The persisted entity after it has been successfully persisted.</returns>
    Task<TEntity> CreateAsync(TEntity entity);
}
