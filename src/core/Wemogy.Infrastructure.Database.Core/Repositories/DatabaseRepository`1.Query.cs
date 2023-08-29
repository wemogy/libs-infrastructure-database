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
        Pagination pagination,
        CancellationToken cancellationToken = default)
    {
        return QueryAsync(
            predicate,
            null,
            pagination,
            cancellationToken);
    }

    public async Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        Sorting<TEntity>? sorting,
        Pagination? pagination,
        CancellationToken cancellationToken = default)
    {
        var entities = new List<TEntity>();

        await IterateAsync(
            predicate,
            sorting,
            pagination,
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
