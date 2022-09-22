using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity, TPartitionKey, TId>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
{
    public Task DeleteAsync(TId id)
    {
        return _database.DeleteAsync(x => id.ToString() == x.Id.ToString());
    }

    public Task DeleteAsync(TId id, TPartitionKey partitionKey)
    {
        return _database.DeleteAsync(
            id,
            partitionKey);
    }

    public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return _database.DeleteAsync(predicate);
    }
}
