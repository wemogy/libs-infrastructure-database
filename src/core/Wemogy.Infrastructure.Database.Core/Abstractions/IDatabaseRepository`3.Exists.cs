using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Abstractions
{
    public partial interface IDatabaseRepository<TEntity, in TPartitionKey, in TId>
        where TEntity : IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>
    {
        Task<bool> ExistsAsync(TId id, TPartitionKey partitionKey, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);

        Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default);
    }
}
