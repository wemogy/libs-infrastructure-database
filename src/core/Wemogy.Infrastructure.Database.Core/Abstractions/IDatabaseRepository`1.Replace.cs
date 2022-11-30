using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Replaces an existing entity in the database.
    /// </summary>
    /// <param name="entity">The updated entity which will replace the existing</param>
    /// <returns>The updated entity as persisted</returns>
    Task<TEntity> ReplaceAsync(TEntity entity);
}
