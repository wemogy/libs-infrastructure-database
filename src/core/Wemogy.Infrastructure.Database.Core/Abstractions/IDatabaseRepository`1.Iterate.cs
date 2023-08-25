using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Iterate through the repository by filtering via a predicate and applying a callback on the retrieved results.
    /// </summary>
    /// <param name="predicate">The predicate to filter the repository for</param>
    /// <param name="callback">The async callback function to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via a predicate and applying a callback on the retrieved results.
    /// </summary>
    /// <param name="predicate">The predicate to filter the repository for</param>
    /// <param name="paginationParameters">Parameters for pagination</param>
    /// <param name="callback">The async callback function to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        PaginationParameters paginationParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via a predicate and applying a callback on the retrieved results.
    /// </summary>
    /// <param name="predicate">The predicate to filter the repository for</param>
    /// <param name="sortingParameters">Parameters for sorting</param>
    /// <param name="callback">The async callback function to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity> sortingParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via a predicate and applying a callback on the retrieved results.
    /// </summary>
    /// <param name="predicate">The predicate to filter the repository for</param>
    /// <param name="sortingParameters">Parameters for sorting</param>
    /// <param name="paginationParameters">Parameters for pagination</param>
    /// <param name="callback">The async callback function to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity> sortingParameters,
        PaginationParameters paginationParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via given query parameters and applying a callback on the retrieved
    ///     results.
    /// </summary>
    /// <param name="queryParameters">The query parameter filter to be used in the query</param>
    /// <param name="callback">The async callback function to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        QueryParameters queryParameters,
        Func<TEntity, Task> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via a predicate and applying a callback on the retrieved results.
    /// </summary>
    /// <param name="predicate">The predicate to filter the repository for</param>
    /// <param name="callback">The callback action to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via a predicate and applying a callback on the retrieved results.
    /// </summary>
    /// <param name="predicate">The predicate to filter the repository for</param>
    /// <param name="paginationParameters">Parameters for pagination</param>
    /// <param name="callback">The callback action to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        PaginationParameters paginationParameters,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via a predicate and applying a callback on the retrieved results.
    /// </summary>
    /// <param name="predicate">The predicate to filter the repository for</param>
    /// <param name="sortingParameters">Parameters for sorting</param>
    /// <param name="callback">The callback action to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity> sortingParameters,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via a predicate and applying a callback on the retrieved results.
    /// </summary>
    /// <param name="predicate">The predicate to filter the repository for</param>
    /// <param name="sortingParameters">Parameters for sorting</param>
    /// <param name="paginationParameters">Parameters for pagination</param>
    /// <param name="callback">The callback action to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        Expression<Func<TEntity, bool>> predicate,
        SortingParameters<TEntity> sortingParameters,
        PaginationParameters paginationParameters,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default);

    /// <summary>
    ///     Iterate through the repository by filtering via given query parameters and applying a callback on the retrieved
    ///     results.
    /// </summary>
    /// <param name="queryParameters">The query parameter filter to be used in the query</param>
    /// <param name="callback">The callback action to apply to each retrieved entity</param>
    /// <param name="cancellationToken">The cancellation token to use for the operation</param>
    Task IterateAsync(
        QueryParameters queryParameters,
        Action<TEntity> callback,
        CancellationToken cancellationToken = default);
}
