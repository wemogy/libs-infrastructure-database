using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : class, IEntityBase
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
        SortingParameters<TEntity> sortingParameters,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            predicate,
            sortingParameters,
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
        SortingParameters<TEntity>? sortingParameters,
        PaginationParameters? paginationParameters,
        CancellationToken cancellationToken = default)
    {
        var entities = new List<TEntity>();

        await IterateAsync(
            predicate,
            sortingParameters,
            paginationParameters,
            entities.Add,
            cancellationToken);

        return entities;
    }

    public async Task<List<TEntity>> QueryAsync(
        QueryParameters queryParameters,
        CancellationToken cancellationToken = default)
    {
        var entities = new List<TEntity>();

        await IterateAsync(
            queryParameters,
            entities.Add,
            cancellationToken);

        return entities;
    }
}
