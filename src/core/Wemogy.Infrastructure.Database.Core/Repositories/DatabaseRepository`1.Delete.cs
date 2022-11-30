using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public async Task DeleteAsync(string id)
    {
        var isDeleted = await _database.DeleteAsync(x => id == x.Id.ToString());
        if (isDeleted == 1)
        {
            return;
        }

        throw DatabaseError.EntityNotFound(id);
    }

    public Task DeleteAsync(string id, string partitionKey)
    {
        return _database.DeleteAsync(
            id,
            partitionKey);
    }

    public Task<int> DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return _database.DeleteAsync(predicate);
    }
}
