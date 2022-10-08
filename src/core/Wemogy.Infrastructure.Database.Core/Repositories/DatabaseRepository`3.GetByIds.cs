using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity, TPartitionKey, TId>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
{
    public Task<List<TEntity>> GetByIdsAsync(List<TId> ids, CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            x => ids.Contains(x.Id),
            cancellationToken);
    }
}
