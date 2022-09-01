using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    public class InMemoryDatabaseClient<TEntity, TPartitionKey, TId> : DatabaseClientBase<TEntity, TPartitionKey, TId>, IDatabaseClient<TEntity, TPartitionKey, TId>
        where TEntity : class, IEntityBase<TId>
        where TPartitionKey : IEquatable<TPartitionKey>
        where TId : IEquatable<TId>
    {
        private static readonly Dictionary<Type, Dictionary<TPartitionKey, List<TEntity>>> Database = new Dictionary<Type, Dictionary<TPartitionKey, List<TEntity>>>();

        private Dictionary<TPartitionKey, List<TEntity>> EntityPartitions => Database[typeof(TEntity)];

        public InMemoryDatabaseClient(InMemoryDatabaseClientOptions options)
        {
            if (!Database.TryGetValue(typeof(TEntity), out var entityPartitions))
            {
                entityPartitions = new Dictionary<TPartitionKey, List<TEntity>>();
                Database.Add(typeof(TEntity), entityPartitions);
            }
        }

        public Task<TEntity> GetAsync(TId id, TPartitionKey partitionKey, CancellationToken cancellationToken)
        {
            if (!EntityPartitions.TryGetValue(partitionKey, out var entities))
            {
                throw DatabaseError.EntityNotFound(id.ToString(), partitionKey.ToString());
            }

            var entity = entities.FirstOrDefault(e => e.Id.Equals(id));

            if (entity == null)
            {
                throw DatabaseError.EntityNotFound(id.ToString(), partitionKey.ToString());
            }

            return Task.FromResult(entity);
        }

        public async Task IterateAsync(
            Expression<Func<TEntity, bool>> predicate,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            foreach (var entityPartition in EntityPartitions)
            {
                foreach (var entity in entityPartition.Value)
                {
                    await callback(entity);
                }
            }
        }

        public Task<TEntity> CreateAsync(TEntity entity)
        {
            var partitionKeyValue = ResolvePartitionKeyValue(entity);

            if (!EntityPartitions.TryGetValue(partitionKeyValue, out var entities))
            {
                entities = new List<TEntity>();
                EntityPartitions.Add(partitionKeyValue, entities);
            }

            if (entities.Any(x => x.Id.Equals(entity.Id)))
            {
                throw Error.Conflict(
                    "AlreadyExists",
                    $"Entity with id {entity.Id} already exists");
            }

            entities.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<TEntity> ReplaceAsync(TEntity entity)
        {
            var partitionKeyValue = ResolvePartitionKeyValue(entity);

            if (!EntityPartitions.TryGetValue(partitionKeyValue, out var entities))
            {
                throw DatabaseError.EntityNotFound(entity.Id.ToString(), partitionKeyValue.ToString());
            }

            var existingEntity = entities.FirstOrDefault(e => e.Id.Equals(entity.Id));

            if (existingEntity == null)
            {
                throw DatabaseError.EntityNotFound(entity.Id.ToString(), partitionKeyValue.ToString());
            }

            entities.Remove(existingEntity);
            entities.Add(entity);

            return Task.FromResult(entity);
        }

        public Task DeleteAsync(TId id, TPartitionKey partitionKey)
        {
            if (!EntityPartitions.TryGetValue(partitionKey, out var entities))
            {
                throw DatabaseError.EntityNotFound(id.ToString(), partitionKey.ToString());
            }

            var entity = entities.FirstOrDefault(e => e.Id.Equals(id));

            if (entity == null)
            {
                throw DatabaseError.EntityNotFound(id.ToString(), partitionKey.ToString());
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
