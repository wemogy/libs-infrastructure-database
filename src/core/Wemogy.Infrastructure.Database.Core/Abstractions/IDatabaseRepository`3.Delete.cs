using System;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity, in TPartitionKey, TId> : IDatabaseRepository
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
{
    Task DeleteAsync(TId id);

    Task DeleteAsync(TId id, TPartitionKey partitionKey);

    Task DeleteAsync(Expression<Func<TEntity, bool>> predicate);
}
