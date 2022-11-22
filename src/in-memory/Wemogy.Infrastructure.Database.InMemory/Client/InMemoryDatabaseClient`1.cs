using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Extensions;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    public class InMemoryDatabaseClient<TEntity> : DatabaseClientBase<TEntity>, IDatabaseClient<TEntity>
        where TEntity : IEntityBase
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

            var entity = entities.FirstOrDefault(e => e.Id.Equals(id));

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
            var predicate = generalFilterPredicate?.Compile() ?? (x => true);
            var queryCondition = queryParameters.GetLambdaExpression<TEntity>();

            var compiledQueryCondition = queryCondition.Compile();
            var predicate1 = predicate;
            predicate = x => predicate1(x) && compiledQueryCondition(x);

            var count = 0;
            return IterateAsync(
                x => predicate(x),
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
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            var compiledPredicate = predicate.Compile();
            foreach (var entityPartition in EntityPartitions)
            {
                foreach (var entity in entityPartition.Value.Where(compiledPredicate))
                {
                    await callback(entity.Clone());
                }
            }
        }

        public Task<TEntity> CreateAsync(TEntity entity)
        {
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

            if (entities.Any(x => x.Id.Equals(entity.Id)))
            {
                throw Error.Conflict(
                    "AlreadyExists",
                    $"Entity with id {entity.Id} already exists");
            }

            entities.Add(entity.Clone());
            return Task.FromResult(entity.Clone());
        }

        public Task<TEntity> ReplaceAsync(TEntity entity)
        {
            var partitionKeyValue = ResolvePartitionKeyValue(entity);

            if (!EntityPartitions.TryGetValue(
                    partitionKeyValue,
                    out var entities))
            {
                throw DatabaseError.EntityNotFound(
                    entity.Id,
                    partitionKeyValue);
            }

            var existingEntity = entities.FirstOrDefault(e => e.Id.Equals(entity.Id));

            if (existingEntity == null)
            {
                throw DatabaseError.EntityNotFound(
                    entity.Id,
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

            var entity = entities.FirstOrDefault(e => e.Id.Equals(id));

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
            var compiledPredicate = predicate.Compile();
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
