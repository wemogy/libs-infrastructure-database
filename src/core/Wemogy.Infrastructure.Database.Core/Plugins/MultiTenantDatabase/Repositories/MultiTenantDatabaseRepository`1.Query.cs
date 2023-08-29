using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            predicate,
            null,
            null,
            cancellationToken);
    }

    public Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        Sorting<TEntity> sorting,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            predicate,
            sorting,
            null,
            cancellationToken);
    }

    public Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        PaginationParameters paginationParameters,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            predicate,
            null,
            paginationParameters,
            cancellationToken);
    }

    public async Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        Sorting<TEntity>? sorting,
        PaginationParameters? paginationParameters,
        CancellationToken cancellationToken = default)
    {
        predicate = BuildComposedPartitionKeyPredicate(predicate);

        List<TEntity> entities;
        if (paginationParameters == null || sorting == null)
        {
            if (sorting != null)
            {
                entities = await _databaseRepository.QueryAsync(
                    predicate,
                    sorting,
                    cancellationToken);
            }
            else if (paginationParameters != null)
            {
                entities = await _databaseRepository.QueryAsync(
                    predicate,
                    paginationParameters,
                    cancellationToken);
            }
            else
            {
                entities = await _databaseRepository.QueryAsync(
                    predicate,
                    cancellationToken);
            }
        }
        else
        {
            entities = await _databaseRepository.QueryAsync(
                predicate,
                sorting,
                paginationParameters,
                cancellationToken);
        }

        foreach (var entity in entities)
        {
            RemovePartitionKeyPrefix(entity);
        }

        return entities;
    }

    public async Task<List<TEntity>> QueryAsync(
        QueryParameters queryParameters,
        CancellationToken cancellationToken = default)
    {
        var queryParametersInternal = GetQueryParametersWithPartitionKeyFilter(queryParameters);

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
