using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Ensure that an entity exists in the repository as found by id and partitionKey, throw if it does not.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to look for</param>
    /// <param name="partitionKey">The unique partitionKey of where the corresponding entity should be located</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has been soft-deleted when the latter is on)
    /// </exception>
    Task EnsureExistsAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Ensure that an entity exists in the repository as found by id, throw if it does not.
    ///     DO NOT PREFER: This method is less efficient, always prefer using an overload with predefined
    ///     partitionKey.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to look for</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has been soft-deleted when the latter is on)
    /// </exception>
    Task EnsureExistsAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Ensure that an entity exists in the repository as found by a predicate, throw if it does not.
    ///     ATTENTION: Including a filter for the partitionKey in the predicate improves performance drastically.
    /// </summary>
    /// <param name="predicate">A filter of the entity to look for.</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has been soft-deleted when this is supported)
    /// </exception>
    Task EnsureExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}
