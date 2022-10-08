using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

public partial class ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity,
    TComposedPrimaryKeyBuilder>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
    where TInternalEntity : IEntityBase<string>
    where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<TId>
{
    public async Task<List<TEntity>> GetByIdsAsync(List<TId> ids, CancellationToken cancellationToken = default)
    {
        var internalIds = ids.Select(BuildComposedPrimaryKey).ToList();
        var internalEntities = await _databaseRepository.GetByIdsAsync(internalIds, cancellationToken);

        var entities = internalEntities.Select(AdaptToEntity).ToList();

        return entities;
    }
}
