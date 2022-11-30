using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Delete an entity from the repository, as identified by its id, or
    ///     set to soft-deleted instead if soft-delete is enabled.
    ///     DO NOT PREFER: This method is less efficient, always prefer using an overload with predefined
    ///     partitionKey.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete</param>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has already been soft-deleted when this is supported)
    /// </exception>
    Task DeleteAsync(string id);

    /// <summary>
    ///     Delete an entity from the repository, as identified by its id and partitionKey, or
    ///     set to soft-deleted instead if soft-delete is enabled.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to delete</param>
    /// <param name="partitionKey">The unique partitionKey of where the corresponding entity is located</param>
    /// <exception cref="NotFoundErrorException">
    ///     Thrown when the entity is not found (either because it does not exist or
    ///     because it has already been soft-deleted when this is supported)
    /// </exception>
    Task DeleteAsync(string id, string partitionKey);

    /// <summary>
    ///     Delete multiple entities from the repository, as identified by a given predicate, or
    ///     set to soft-deleted instead if soft-delete is enabled.
    ///     ATTENTION: Including a filter for the partitionKey in the predicate improves performance drastically.
    /// </summary>
    /// <param name="predicate">The predicate to filter for the corresponding entity to delete.</param>
    /// <returns>The number of (soft-)deleted entities, 0 if none</returns>
    Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate);
}
