using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Check if an entity exists in the repository as identified by its id and partitionKey.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to look for</param>
    /// <param name="partitionKey">The unique partitionKey of where the corresponding entity should be located</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>True if an entity exists in the repository as found by id and partitionKey, false otherwise</returns>
    Task<bool> ExistsAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if an entity exists in the repository as identified by its id.
    ///     DO NOT PREFER: This method is less efficient, always prefer using an overload with predefined
    ///     partitionKey.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to look for</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>True if an entity exists in the repository as found by id and partitionKey, false otherwise</returns>
    Task<bool> ExistsAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Check if an entity exists in the repository as identified by a predicate.
    ///     ATTENTION: Including a filter for the partitionKey in the predicate improves performance drastically.
    /// </summary>
    /// <param name="predicate">A filter of the entity to look for.</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>True if an entity exists in the repository as found by id and partitionKey, false otherwise</returns>
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}
