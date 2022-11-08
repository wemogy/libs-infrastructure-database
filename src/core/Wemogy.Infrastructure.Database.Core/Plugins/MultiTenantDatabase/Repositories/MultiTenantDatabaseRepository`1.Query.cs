using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public async Task<List<TEntity>> QueryAsync(Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = await _databaseRepository.QueryAsync(
            PartitionKeyPredicate.And(predicate),
            cancellationToken);

        foreach (var entity in entities)
        {
            RemovePartitionKeyPrefix(entity);
        }

        return entities;
    }

    public async Task<List<TEntity>> QueryAsync(QueryParameters queryParameters,
        CancellationToken cancellationToken = default)
    {
        var queryParametersInternal = queryParameters.Clone();

        var partitionKeyPrefixFilter = new QueryFilter
        {
            Comparator = Comparator.StartsWithIgnoreCase,
            Value = _databaseTenantProvider.GetTenantId(),
            Property = _partitionKeyProperty.Name
        };

        queryParametersInternal.Filters.Add(partitionKeyPrefixFilter);

        var entities = await _databaseRepository.QueryAsync(
            queryParametersInternal,
            cancellationToken);

        foreach (var entity in entities)
        {
            RemovePartitionKeyPrefix(entity);
        }

        return entities;
    }
}
