using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Query the repository by a predicate for a SINGLE entity. Throws when the result is not single.
    ///     ATTENTION: Including a filter for the partitionKey in the predicate improves performance drastically.
    /// </summary>
    /// <param name="predicate">A filter of the entity to look for.</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The entity as found in the repository</returns>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has been soft-deleted when this is supported)
    /// </exception>
    /// <exception cref="PreconditionFailedErrorException">
    ///     Thrown when more than results are found in the repository
    /// </exception>
    Task<TEntity> QuerySingleAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
}
