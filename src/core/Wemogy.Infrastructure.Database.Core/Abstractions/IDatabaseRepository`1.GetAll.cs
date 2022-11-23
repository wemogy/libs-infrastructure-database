using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Retrieve all entities from the repository.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>A list of entities as found in the repository</returns>
    Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);
}
