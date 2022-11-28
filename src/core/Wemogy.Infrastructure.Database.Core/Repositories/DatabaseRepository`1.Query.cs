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
    public async Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = new List<TEntity>();

        await IterateAsync(
            predicate,
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
