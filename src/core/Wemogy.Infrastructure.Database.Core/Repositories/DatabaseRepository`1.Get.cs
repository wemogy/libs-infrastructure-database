using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity>
    where TEntity : IEntityBase
{
    public async Task<TEntity> GetAsync(
        string id,
        string partitionKey,
        CancellationToken cancellationToken = default)
    {
        var entity = await _database.GetAsync(
            id,
            partitionKey,
            cancellationToken);

        // Throw exception if soft delete is enabled and entity is deleted
        if (SoftDelete.IsEnabled && IsSoftDeleted(entity))
        {
            throw DatabaseError.EntityNotFound(
                id,
                partitionKey);
        }

        var filter = await GetReadFilter();

        if (!filter(entity))
        {
            throw DatabaseError.EntityNotFound(
                id,
                partitionKey);
        }

        await PropertyFilters.ApplyAsync(entity);

        return entity;
    }

    public async Task<TEntity> GetAsync(string id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await GetAsync(
                x => x.Id.ToString() == id,
                cancellationToken);
        }
        catch (NotFoundErrorException)
        {
            throw DatabaseError.EntityNotFound(id);
        }
    }

    public async Task<TEntity> GetAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var items = await QueryAsync(
            predicate,
            cancellationToken);

        // throw exception if no item found
        if (!items.Any())
        {
            throw DatabaseError.EntityNotFound(predicate.ToString());
        }

        var entity = items.First();

        // Throw exception if soft delete is enabled and entity is deleted
        if (SoftDelete.IsEnabled && IsSoftDeleted(entity))
        {
            throw DatabaseError.EntityNotFound(predicate.ToString());
        }

        await PropertyFilters.ApplyAsync(entity);

        return entity;
    }
}
