using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    public Task DeleteAsync(string id)
    {
        return _database.DeleteAsync(x => id == x.Id.ToString());
    }

    public Task DeleteAsync(string id, string partitionKey)
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
