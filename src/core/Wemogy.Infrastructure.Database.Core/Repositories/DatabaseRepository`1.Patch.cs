using System;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
{
    public Task<TEntity> PatchAsync(
        string id,
        PartitionKeyValue partitionKey,
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
