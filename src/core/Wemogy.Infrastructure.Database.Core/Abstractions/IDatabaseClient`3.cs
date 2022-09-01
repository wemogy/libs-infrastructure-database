using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public interface IDatabaseClient<TEntity, in TPartitionKey, in TId> : IDatabaseClient
        where TEntity : IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>
    {
        Task<TEntity> GetAsync(TId id, TPartitionKey partitionKey, CancellationToken cancellationToken);

        /// <summary>
        /// Iterates over all items which matches the predicate.
        /// </summary>
        Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Func<TEntity, Task> callback, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new entity.
        /// </summary>
        Task<TEntity> CreateAsync(TEntity entity);

        /// <summary>
        /// Replaces an existing entity in the database.
        /// </summary>
        Task<TEntity> ReplaceAsync(TEntity entity);

        Task DeleteAsync(TId id, TPartitionKey partitionKey);

        Task DeleteAsync(Expression<Func<TEntity, bool>> predicate);
    }
}
