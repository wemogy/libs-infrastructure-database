using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Retrieve an entity from the repository by its unique identifier and partition key.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to look for</param>
    /// <param name="partitionKey">The unique partitionKey of where the corresponding entity should be located</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The entity as found in the repository</returns>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has been soft-deleted when this is supported)
    /// </exception>
    Task<TEntity> GetAsync(string id, string partitionKey, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieve an entity from the repository by its unique identifier.
    ///     DO NOT PREFER: This method is less efficient, always prefer using an overload with predefined
    ///     partitionKey.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to look for</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The entity as found in the repository</returns>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has been soft-deleted when this is supported)
    /// </exception>
    Task<TEntity> GetAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    ///     Retrieve an entity from the repository by a predicate.
    ///     ATTENTION: Including a filter for the partitionKey in the predicate improves performance drastically.
    /// </summary>
    /// <param name="predicate">A filter of the entity to look for.</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The entity as found in the repository</returns>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has been soft-deleted when this is supported)
    /// </exception>
    Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}
