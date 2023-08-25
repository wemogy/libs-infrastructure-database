using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Query the repository based on a given predicate.
    /// </summary>
    /// <param name="predicate">A filter of the entities to query for.</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The corresponding entities as found in the repository</returns>
    Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Query the repository based on a given predicate.
    /// </summary>
    /// <param name="predicate">A filter of the entities to query for.</param>
    /// <param name="sortingParameters">Parameters for sorting</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The corresponding entities as found in the repository</returns>
    Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity> sortingParameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Query the repository based on a given predicate.
    /// </summary>
    /// <param name="predicate">A filter of the entities to query for.</param>
    /// <param name="paginationParameters">Parameters for pagination</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The corresponding entities as found in the repository</returns>
    Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        PaginationParameters paginationParameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Query the repository based on a given predicate.
    /// </summary>
    /// <param name="predicate">A filter of the entities to query for.</param>
    /// <param name="sortingParameters">Parameters for sorting</param>
    /// <param name="paginationParameters">Parameters for pagination</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The corresponding entities as found in the repository</returns>
    Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity> sortingParameters,
        PaginationParameters paginationParameters,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Query the repository based on given query parameters.
    /// </summary>
    /// <param name="queryParameters">The query parameters that define the filter to query for</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    /// <returns>The corresponding entities as found in the repository</returns>
    Task<List<TEntity>> QueryAsync(QueryParameters queryParameters, CancellationToken cancellationToken = default);
}
