using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public Task<TEntity> PatchAsync(
        string id,
        string partitionKey,
        Action<IPatchOperations<TEntity>> operations,
        Expression<Func<TEntity, bool>>? condition = null,
        CancellationToken cancellationToken = default)
    {
        return _database.PatchAsync(
            id,
            partitionKey,
            operations,
            condition,
            cancellationToken);
    }
}
