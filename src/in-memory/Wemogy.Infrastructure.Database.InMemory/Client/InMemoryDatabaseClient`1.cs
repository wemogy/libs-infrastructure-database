using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FastExpressionCompiler;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Extensions;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    public class InMemoryDatabaseClient<TEntity> : DatabaseClientBase<TEntity>, IDatabaseClient<TEntity>
        where TEntity : class
    {
        private static readonly Dictionary<Type, Dictionary<string, List<TEntity>>> Database =
            new Dictionary<Type, Dictionary<string, List<TEntity>>>();

        public InMemoryDatabaseClient()
        {
            if (!Database.TryGetValue(
                    typeof(TEntity),
                    out var entityPartitions))
            {
                entityPartitions = new Dictionary<string, List<TEntity>>();
                Database.Add(
                    typeof(TEntity),
                    entityPartitions);
            }
        }

        private Dictionary<string, List<TEntity>> EntityPartitions => Database[typeof(TEntity)];

        public Task<TEntity> GetAsync(string id, string partitionKey, CancellationToken cancellationToken)
        {
            if (!EntityPartitions.TryGetValue(
                    partitionKey,
                    out var entities))
            {
                throw DatabaseError.EntityNotFound(
                    id,
                    partitionKey);
            }

            TEntity entity = entities.AsQueryable().FirstOrDefault("e => e.Id.Equals(@0)", id);

            if (entity == null)
            {
                throw DatabaseError.EntityNotFound(
                    id,
                    partitionKey);
            }

            return Task.FromResult(entity.Clone());
        }

        public Task IterateAsync(
            QueryParameters queryParameters,
            Expression<Func<TEntity, bool>>? generalFilterPredicate,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            var predicate = generalFilterPredicate?.CompileFast() ?? (x => true);
            var queryCondition = queryParameters.GetLambdaExpression<TEntity>();

            var compiledQueryCondition = queryCondition.CompileFast();
            var predicate1 = predicate;
            predicate = x => predicate1(x) && compiledQueryCondition(x);

            var count = 0;
            return IterateAsync(
                x => predicate(x),
                null,
                entity =>
                {
                    if (queryParameters.Take.HasValue && count++ < queryParameters.Take)
                    {
                        return Task.CompletedTask;
                    }

                    return callback(entity.Clone());
                },
                cancellationToken);
        }

        public async Task IterateAsync(
            Expression<Func<TEntity, bool>> predicate,
            PaginationParameters? paginationParameters,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            var compiledPredicate = predicate.CompileFast();
            foreach (var entityPartition in EntityPartitions)
            {
                var queryable = entityPartition.Value.Where(compiledPredicate);
                if (paginationParameters != null)
                {
                    queryable = queryable.Skip(paginationParameters.Skip).Take(paginationParameters.Take);
                }

                foreach (var entity in queryable)
                {
                    await callback(entity.Clone());
                }
            }
        }

        public Task<TEntity> CreateAsync(TEntity entity)
        {
            var id = ResolveIdValue(entity);
            var partitionKeyValue = ResolvePartitionKeyValue(entity);

            if (!EntityPartitions.TryGetValue(
                    partitionKeyValue,
                    out var entities))
            {
                entities = new List<TEntity>();
                EntityPartitions.Add(
                    partitionKeyValue,
                    entities);
            }

            if (entities.AsQueryable().Any("x => x.Id.Equals(@0)", id))
            {
                throw Error.Conflict(
                    "AlreadyExists",
                    $"Entity with id {id} already exists");
            }

            entities.Add(entity.Clone());
            return Task.FromResult(entity.Clone());
        }

        public Task<TEntity> ReplaceAsync(TEntity entity)
        {
            var id = ResolveIdValue(entity);
            var partitionKeyValue = ResolvePartitionKeyValue(entity);

            if (!EntityPartitions.TryGetValue(
                    partitionKeyValue,
                    out var entities))
            {
                throw DatabaseError.EntityNotFound(
                    id,
                    partitionKeyValue);
            }

            var existingEntity = entities.AsQueryable().FirstOrDefault("e => e.Id.Equals(@0)", id);

            if (existingEntity == null)
            {
                throw DatabaseError.EntityNotFound(
                    id,
                    partitionKeyValue);
            }

            entities.Remove(existingEntity);
            entities.Add(entity.Clone());

            return Task.FromResult(entity.Clone());
        }

        public Task DeleteAsync(string id, string partitionKey)
        {
            if (!EntityPartitions.TryGetValue(
                    partitionKey,
                    out var entities))
            {
                throw DatabaseError.EntityNotFound(
                    id,
                    partitionKey);
            }

            var entity = entities.AsQueryable().FirstOrDefault("e => e.Id.Equals(@0)", id);

            if (entity == null)
            {
                throw DatabaseError.EntityNotFound(
                    id,
                    partitionKey);
            }

            entities.Remove(entity);
            return Task.CompletedTask;
        }

        public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var compiledPredicate = predicate.CompileFast();
            foreach (var entityPartition in EntityPartitions)
            {
                var entities = entityPartition.Value.Where(compiledPredicate).ToList();

                foreach (var entity in entities)
                {
                    entityPartition.Value.Remove(entity);
                }
            }

            return Task.CompletedTask;
        }
    }
}
