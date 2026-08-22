using System;
using System.Collections.Generic;
using System.Linq;
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
using Wemogy.Infrastructure.Database.InMemory.Query;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    public class InMemoryDatabaseClient<TEntity> : DatabaseClientBase<TEntity>, IDatabaseClient<TEntity>
        where TEntity : class
    {
        /// <summary>
        ///     The store is static, so every client, factory and repository for this entity type
        ///     shares one database - the in-memory provider stands in for a single database, and two
        ///     repositories over the same entity have to see the same data. Because the type is
        ///     generic, each closed generic type gets its own store.
        /// </summary>
        private static readonly Dictionary<string, List<TEntity>> Partitions =
            new Dictionary<string, List<TEntity>>();

        /// <summary>
        ///     Guards <see cref="Partitions"/> and every entity list inside it. Clients are
        ///     typically registered as singletons, so concurrent requests would otherwise corrupt
        ///     the dictionaries.
        /// </summary>
        private static readonly object Gate = new object();

        public Task<TEntity> GetAsync(string id, string partitionKey, CancellationToken cancellationToken)
        {
            lock (Gate)
            {
                var entity = FindEntity(
                    partitionKey,
                    id);

                if (entity == null)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey,
                        hint: typeof(TEntity).Name);
                }

                return Task.FromResult(entity.Clone());
            }
        }

        public async Task IterateAsync(
            QueryParameters queryParameters,
            Expression<Func<TEntity, bool>>? generalFilterPredicate,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            var generalFilter = generalFilterPredicate?.CompileFast();
            var queryCondition = queryParameters.GetLambdaExpression<TEntity>().CompileFast();

            var entities = Snapshot(x => (generalFilter == null || generalFilter(x)) && queryCondition(x));

            entities = InMemoryQueryOrdering.ApplySortings(
                entities,
                queryParameters);
            entities = InMemoryQueryOrdering.ApplySearchAfter(
                entities,
                queryParameters);

            if (queryParameters.Take.HasValue)
            {
                entities = entities
                    .Take(queryParameters.Take.Value)
                    .ToList();
            }

            foreach (var entity in entities)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await callback(entity.Clone());
            }
        }

        public async Task IterateAsync(
            Expression<Func<TEntity, bool>> predicate,
            Sorting<TEntity>? sorting,
            Pagination? pagination,
            Func<TEntity, Task> callback,
            CancellationToken cancellationToken)
        {
            var entities = Snapshot(predicate.CompileFast());

            // the sorting has to be applied to the whole result set. Applying it per partition
            // would make both the order and every page built from it depend on the partition layout
            IEnumerable<TEntity> results = sorting != null
                ? sorting.ApplyTo(entities)
                : entities;

            if (pagination != null)
            {
                results = results
                    .Skip(pagination.Skip)
                    .Take(pagination.Take);
            }

            foreach (var entity in results)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await callback(entity.Clone());
            }
        }

        public Task<long> CountAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken cancellationToken)
        {
            var compiledPredicate = predicate.CompileFast();

            lock (Gate)
            {
                return Task.FromResult(Partitions.Values.Sum(entities => entities.LongCount(compiledPredicate)));
            }
        }

        public Task<TEntity> CreateAsync(TEntity entity)
        {
            var id = ResolveIdValue(entity);
            var partitionKeyValue = ResolvePartitionKeyValue(entity);

            lock (Gate)
            {
                if (FindEntity(
                        partitionKeyValue,
                        id) != null)
                {
                    throw Error.Conflict(
                        "AlreadyExists",
                        $"Entity with id {id} already exists");
                }

                var eTag = NextETag();
                GetOrCreatePartition(partitionKeyValue).Add(Copy(entity, eTag));

                return Task.FromResult(Copy(entity, eTag));
            }
        }

        public Task<TEntity> ReplaceAsync(TEntity entity)
        {
            var id = ResolveIdValue(entity);
            var partitionKeyValue = ResolvePartitionKeyValue(entity);

            lock (Gate)
            {
                var index = FindEntityIndex(
                    partitionKeyValue,
                    id);

                if (index < 0)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKeyValue,
                        hint: typeof(TEntity).Name);
                }

                var entities = Partitions[partitionKeyValue];

                EnsureETagMatches(
                    entity,
                    entities[index],
                    id,
                    partitionKeyValue);

                var eTag = NextETag();

                // replaced in place, so an iteration of this partition keeps the insertion order
                entities[index] = Copy(entity, eTag);

                return Task.FromResult(Copy(entity, eTag));
            }
        }

        public Task<TEntity> UpsertAsync(TEntity entity)
        {
            return UpsertAsync(
                entity,
                ResolvePartitionKeyValue(entity));
        }

        public Task<TEntity> UpsertAsync(TEntity entity, string partitionKey)
        {
            var id = ResolveIdValue(entity);

            lock (Gate)
            {
                var entities = GetOrCreatePartition(partitionKey);
                var index = entities.FindIndex(x => ResolveIdValue(x) == id);
                var eTag = NextETag();

                // an upsert carries no precondition, mirroring a Cosmos upsert without IfMatch
                if (index < 0)
                {
                    entities.Add(Copy(entity, eTag));
                }
                else
                {
                    entities[index] = Copy(entity, eTag);
                }

                return Task.FromResult(Copy(entity, eTag));
            }
        }

        public Task DeleteAsync(string id, string partitionKey)
        {
            lock (Gate)
            {
                var entity = FindEntity(
                    partitionKey,
                    id);

                if (entity == null)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey,
                        hint: typeof(TEntity).Name);
                }

                Partitions[partitionKey].Remove(entity);
                return Task.CompletedTask;
            }
        }

        public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var compiledPredicate = predicate.CompileFast();

            lock (Gate)
            {
                foreach (var entities in Partitions.Values)
                {
                    entities.RemoveAll(x => compiledPredicate(x));
                }

                return Task.CompletedTask;
            }
        }

        /// <summary>
        ///     Materializes the matching entities of all partitions under the lock, so the callbacks
        ///     of the callers run without holding it and can write to the repository while iterating.
        /// </summary>
        private static List<TEntity> Snapshot(Func<TEntity, bool> predicate)
        {
            lock (Gate)
            {
                return Partitions.Values
                    .SelectMany(entities => entities)
                    .Where(predicate)
                    .ToList();
            }
        }

        private static List<TEntity> GetOrCreatePartition(string partitionKey)
        {
            if (!Partitions.TryGetValue(
                    partitionKey,
                    out var entities))
            {
                entities = new List<TEntity>();
                Partitions.Add(
                    partitionKey,
                    entities);
            }

            return entities;
        }

        private TEntity? FindEntity(string partitionKey, string id)
        {
            var index = FindEntityIndex(
                partitionKey,
                id);
            return index < 0 ? null : Partitions[partitionKey][index];
        }

        private int FindEntityIndex(string partitionKey, string id)
        {
            if (!Partitions.TryGetValue(
                    partitionKey,
                    out var entities))
            {
                return -1;
            }

            return entities.FindIndex(x => ResolveIdValue(x) == id);
        }

        /// <summary>
        ///     Returns an independent copy of the entity, stamped with the given eTag. Both the
        ///     stored and the returned entity are copies, so neither the caller nor a later read can
        ///     mutate the store by holding on to an instance.
        /// </summary>
        private TEntity Copy(TEntity entity, string? eTag)
        {
            var copy = entity.Clone();
            SetETagValue(
                copy,
                eTag);
            return copy;
        }

        private string? NextETag()
        {
            return SupportsETag ? $"\"{Guid.NewGuid()}\"" : null;
        }

        private void EnsureETagMatches(TEntity entity, TEntity existingEntity, string id, string partitionKey)
        {
            var eTag = ResolveETagValue(entity);

            // a caller that did not read an eTag does not ask for a precondition, which mirrors
            // Cosmos' behaviour for a null IfMatchEtag
            if (string.IsNullOrEmpty(eTag))
            {
                return;
            }

            if (eTag != ResolveETagValue(existingEntity))
            {
                throw Error.PreconditionFailed(
                    "EtagMismatch",
                    $"The eTag of the entity with id {id} and partition key {partitionKey} does not match the version in the database");
            }
        }
    }
}
