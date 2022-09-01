using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

[assembly: InternalsVisibleTo("Wemogy.Infrastructure.Database.Core.UnitTests")]
[assembly: InternalsVisibleTo("Wemogy.Infrastructure.Database.Cosmos.UnitTests")]

namespace Wemogy.Infrastructure.Database.Core.Repositories;

public class DatabaseRepository<TEntity, TPartitionKey, TId> : IDatabaseRepository<TEntity, TPartitionKey, TId>
    where TEntity : IEntityBase<TId>
    where TPartitionKey : IEquatable<TPartitionKey>
    where TId : IEquatable<TId>
{
    private IDatabaseClient<TEntity, TPartitionKey, TId> _database;

    public SoftDeleteState SoftDelete { get; }

    private readonly List<IDatabaseRepositoryReadFilter<TEntity>> _queryFilters;

    public DatabaseRepository(
        IDatabaseClient<TEntity, TPartitionKey, TId> database,
        DatabaseRepositoryOptions options,
        List<IDatabaseRepositoryReadFilter<TEntity>> queryFilters)
    {
        _database = database;
        _queryFilters = queryFilters;
        SoftDelete = new SoftDeleteState(options.EnableSoftDelete);
    }

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
        if (SoftDelete.IsEnabled && entity.Deleted)
        {
            throw DatabaseError.EntityNotFound(
                id.ToString(),
                partitionKey.ToString());
        }

        var filter = await GetQueryFilter();

        if (!filter(entity))
        {
            throw DatabaseError.EntityNotFound(
                id.ToString(),
                partitionKey.ToString());
        }

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
        if (SoftDelete.IsEnabled && entity.Deleted)
        {
            throw DatabaseError.EntityNotFound(predicate.ToString());
        }

        return entity;
    }

    public async Task<List<TEntity>> QueryAsync(
        Expression<Func<TEntity, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        var entities = new List<TEntity>();

        await _database.IterateAsync(
            predicate,
            entity =>
            {
                entities.Add(entity);
                return Task.CompletedTask;
            },
            cancellationToken);

        return entities;
    }

    public Task<List<TEntity>> QueryAsync(
        QueryParameters queryParameters,
        CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<TEntity> CreateAsync(TEntity entity)
    {
        return _database.CreateAsync(entity);
    }

    public Task<TEntity> ReplaceAsync(TEntity entity)
    {
        return _database.ReplaceAsync(entity);
    }

    public Task<TEntity> UpdateAsync(TId id, TPartitionKey partitionKey, Action<TEntity> updateAction)
    {
        return UpdateAsync(
            id,
            partitionKey,
            entity =>
            {
                updateAction(entity);
                return Task.CompletedTask;
            });
    }

    public Task<TEntity> UpdateAsync(TId id, Action<TEntity> updateAction)
    {
        return UpdateAsync(
            id,
            entity =>
            {
                updateAction(entity);
                return Task.CompletedTask;
            });
    }

    public async Task<TEntity> UpdateAsync(TId id, TPartitionKey partitionKey, Func<TEntity, Task> updateAction)
    {
        var entity = await GetAsync(
            id,
            partitionKey);
        await updateAction(entity);
        var updatedEntity = await _database.ReplaceAsync(entity);
        return updatedEntity;
    }

    public async Task<TEntity> UpdateAsync(TId id, Func<TEntity, Task> updateAction)
    {
        var entity = await GetAsync(id);
        await updateAction(entity);
        var updatedEntity = await _database.ReplaceAsync(entity);
        return updatedEntity;
    }

    public Task DeleteAsync(TId id)
    {
        return _database.DeleteAsync(x => id.ToString() == x.Id.ToString());
    }

    public Task DeleteAsync(TId id, TPartitionKey partitionKey)
    {
        return _database.DeleteAsync(
            id,
            partitionKey);
    }

    public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        return _database.DeleteAsync(predicate);
    }

    private async Task<Func<TEntity, bool>> GetQueryFilter()
    {
        Expression<Func<TEntity, bool>> defaultFilter = x => true;
        var combinedExpressionBody = defaultFilter;
        foreach (var queryFilter in _queryFilters)
        {
            var filterExpression = await queryFilter.FilterAsync();
            combinedExpressionBody = combinedExpressionBody.And(filterExpression);
        }

        // var lambda = Expression.Lambda<Func<TEntity, bool>>(combinedExpressionBody, defaultFilter.Parameters[0]);
        return combinedExpressionBody.Compile();
    }

    internal IDatabaseClient<TEntity, TPartitionKey, TId> GetDatabaseClient()
    {
        return _database;
    }

    internal void SetDatabaseClient(IDatabaseClient<TEntity, TPartitionKey, TId> database)
    {
        _database = database;
    }
}
