using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Update an entity in the database as retrieved by a unique identifier and partition key.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to update</param>
    /// <param name="partitionKey">The unique partitionKey of where the corresponding entity should be located</param>
    /// <param name="updateAction">The update action to execute</param>
    /// <returns>The updated entity as persisted</returns>
    Task<TEntity> UpdateAsync(string id, string partitionKey, Action<TEntity> updateAction);

    /// <summary>
    ///     Update an entity in the database as retrieved by a unique identifier.
    ///     DO NOT PREFER: This method is less efficient, always prefer using an overload with predefined
    ///     partitionKey.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to update</param>
    /// <param name="updateAction">The update action to execute</param>
    /// <returns>The updated entity as persisted</returns>
    Task<TEntity> UpdateAsync(string id, Action<TEntity> updateAction);

    /// <summary>
    ///     Update an entity in the database as retrieved by a unique identifier and partition key.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to update</param>
    /// <param name="partitionKey">The unique partitionKey of where the corresponding entity should be located</param>
    /// <param name="updateAction">The async update function to execute</param>
    /// <returns>The updated entity as persisted</returns>
    Task<TEntity> UpdateAsync(string id, string partitionKey, Func<TEntity, Task> updateAction);

    /// <summary>
    ///     Update an entity in the database as retrieved by a unique identifier.
    ///     DO NOT PREFER: This method is less efficient, always prefer using an overload with predefined
    ///     partitionKey.
    /// </summary>
    /// <param name="id">The unique identifier of the entity to update</param>
    /// <param name="updateAction">The async update function to execute</param>
    /// <returns>The updated entity as persisted</returns>
    Task<TEntity> UpdateAsync(string id, Func<TEntity, Task> updateAction);
}
