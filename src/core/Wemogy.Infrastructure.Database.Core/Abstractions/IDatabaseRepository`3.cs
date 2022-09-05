using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public partial interface IDatabaseRepository<TEntity, in TPartitionKey, in TId> : IDatabaseRepository
        where TEntity : IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>
    {
        SoftDeleteState<TEntity> SoftDelete { get; }

        Task<TEntity> GetAsync(TId id, TPartitionKey partitionKey, CancellationToken cancellationToken = default);

        Task<TEntity> GetAsync(TId id, CancellationToken cancellationToken = default);

        Task<TEntity> GetAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

        Task<List<TEntity>> GetAllAsync(CancellationToken cancellationToken = default);

        Task<List<TEntity>> QueryAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);

        Task<List<TEntity>> QueryAsync(QueryParameters queryParameters, CancellationToken cancellationToken = default);

        Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Func<TEntity, Task> callback, CancellationToken cancellationToken = default);

        Task IterateAsync(QueryParameters queryParameters, Func<TEntity, Task> callback, CancellationToken cancellationToken = default);

        Task IterateAsync(Expression<Func<TEntity, bool>> predicate, Action<TEntity> callback, CancellationToken cancellationToken = default);

        Task IterateAsync(QueryParameters queryParameters, Action<TEntity> callback, CancellationToken cancellationToken = default);

        Task<TEntity> CreateAsync(TEntity entity);

        Task<TEntity> ReplaceAsync(TEntity entity);

        Task<TEntity> UpdateAsync(TId id, TPartitionKey partitionKey, Action<TEntity> updateAction);

        Task<TEntity> UpdateAsync(TId id, Action<TEntity> updateAction);

        Task<TEntity> UpdateAsync(TId id, TPartitionKey partitionKey, Func<TEntity, Task> updateAction);

        Task<TEntity> UpdateAsync(TId id, Func<TEntity, Task> updateAction);

        Task DeleteAsync(TId id);

        Task DeleteAsync(TId id, TPartitionKey partitionKey);

        Task DeleteAsync(Expression<Func<TEntity, bool>> predicate);
    }
}
