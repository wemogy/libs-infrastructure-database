using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Retrieve multiple entities from the repository based on their ids.
    /// </summary>
    /// <param name="ids">A list of unique identifiers to query for</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>A list of entities as found in the repository</returns>
    Task<List<TEntity>> GetByIdsAsync(List<string> ids, CancellationToken cancellationToken = default);
}
