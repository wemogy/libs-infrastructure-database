using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Mapster;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Repositories;

public partial class ComposedPrimaryKeyDatabaseRepository<TEntity, TPartitionKey, TId, TInternalEntity, TComposedPrimaryKeyBuilder>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
    where TInternalEntity : IEntityBase<string>
    where TComposedPrimaryKeyBuilder : IComposedPrimaryKeyBuilder<TId>
{
    public async Task<List<TEntity>> QueryAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken = default)
    {
        var internalEntityExpression = AdaptToInternalEntityExpression(predicate);
        var internalEntities = await _databaseRepository.QueryAsync(internalEntityExpression, cancellationToken);

        if (internalEntities is List<TEntity> entities)
        {
            return entities;
        }

        entities = internalEntities.Select(AdaptToEntity).ToList();

        return entities;
    }

    public async Task<List<TEntity>> QueryAsync(QueryParameters queryParameters, CancellationToken cancellationToken = default)
    {
        var internalQueryParameters = AdaptToInternalEntityQueryParameters(queryParameters);
        var internalEntities = await _databaseRepository.QueryAsync(internalQueryParameters, cancellationToken);

        if (internalEntities is List<TEntity> entities)
        {
            return entities;
        }

        entities = internalEntities.Select(AdaptToEntity).ToList();

        return entities;
    }
}
