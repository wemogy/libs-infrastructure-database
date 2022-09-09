using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public partial class DatabaseRepository<TEntity, TPartitionKey, TId>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
{
    public async Task<TEntity> GetAsync(
        TId id,
        TPartitionKey partitionKey,
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
                id.ToString(),
                partitionKey.ToString());
        }

        var filter = await GetReadFilter();

        if (!filter(entity))
        {
            throw DatabaseError.EntityNotFound(
                id.ToString(),
                partitionKey.ToString());
        }

        await PropertyFilters.ApplyAsync(entity);

        return entity;
    }

    public Task<TEntity> GetAsync(TId id, CancellationToken cancellationToken = default)
    {
        return GetAsync(
            x => x.Id.ToString() == id.ToString(),
            cancellationToken);
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
