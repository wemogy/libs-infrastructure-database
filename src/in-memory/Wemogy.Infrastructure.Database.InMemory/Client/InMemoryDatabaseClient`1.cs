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
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;
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

        public IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(string partitionKey)
        {
            return new InMemoryTransactionalBatch<TEntity>(
                this,
                partitionKey,
                ResolvePartitionKeyValue);
        }

        public Task<TEntity> PatchAsync(
            string id,
            string partitionKey,
            Action<IPatchOperations<TEntity>> operations,
            Expression<Func<TEntity, bool>>? condition,
            CancellationToken cancellationToken)
        {
            try
            {
                // a patch that is applied in process still must not touch the store after the
                // caller cancelled, the way the Cosmos provider does not once it passes the token on
                cancellationToken.ThrowIfCancellationRequested();

                // collected and compiled inside the try as well, so a rejected path or an empty
                // patch faults the returned task like every other failure of this method does
                var patchOperations = PatchOperationsBuilder<TEntity>.Build(operations);

                // the condition is compiled and evaluated in process. That accepts more than the
                // Cosmos provider does, whose LINQ provider has to translate the condition into SQL
                // - a condition that passes here can still be rejected there, which is what the
                // Cosmos tests are for
                var compiledCondition = condition?.CompileFast();

                lock (Gate)
                {
                    var index = FindEntityIndex(
                        partitionKey,
                        id);

                    if (index < 0)
                    {
                        throw DatabaseError.EntityNotFound(
                            id,
                            partitionKey,
                            hint: typeof(TEntity).Name);
                    }

                    var entities = Partitions[partitionKey];
                    var patchedEntity = BuildPatchedEntity(
                        entities[index],
                        id,
                        partitionKey,
                        patchOperations,
                        compiledCondition,
                        null);

                    // replaced in place, so an iteration of this partition keeps the insertion order
                    entities[index] = patchedEntity;

                    return Task.FromResult(patchedEntity.Clone());
                }
            }
            catch (Exception e)
            {
                // the patch is applied in process, so without this the failure would be thrown
                // before the task is even returned, while the Cosmos provider faults the task. A
                // caller that composes patches with Task.WhenAll would see two behaviours
                return Task.FromException<TEntity>(e);
            }
        }

        /// <summary>
        ///     Applies the operations of a transactional batch atomically. Every operation is
        ///     validated against a working copy of the partition first and the copy only replaces
        ///     the stored partition once all of them passed, so a failing batch cannot leave a
        ///     partial write behind and needs no rollback.
        ///     <para>
        ///         The operations are validated in order against the state at execute time, so a
        ///         create followed by a replace of the same id inside one batch is valid.
        ///     </para>
        /// </summary>
        /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
        /// <param name="operations">The recorded operations, in the order they were added</param>
        internal void ExecuteBatch(
            string partitionKey,
            IReadOnlyList<InMemoryTransactionalBatchOperation<TEntity>> operations)
        {
            lock (Gate)
            {
                var workingCopy = Partitions.TryGetValue(
                    partitionKey,
                    out var entities)
                    ? new List<TEntity>(entities)
                    : new List<TEntity>();

                for (var index = 0; index < operations.Count; index++)
                {
                    ApplyBatchOperation(
                        workingCopy,
                        operations[index],
                        index,
                        partitionKey);
                }

                Partitions[partitionKey] = workingCopy;
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

        private void ApplyBatchOperation(
            List<TEntity> entities,
            InMemoryTransactionalBatchOperation<TEntity> operation,
            int operationIndex,
            string partitionKey)
        {
            if (operation.Kind == InMemoryTransactionalBatchOperationKind.Delete)
            {
                var idToDelete = operation.Id!;
                var indexToDelete = FindEntityIndex(
                    entities,
                    idToDelete);

                if (indexToDelete < 0)
                {
                    throw TransactionalBatchError.EntityNotFound(
                        operationIndex,
                        idToDelete,
                        partitionKey,
                        typeof(TEntity).Name);
                }

                entities.RemoveAt(indexToDelete);
                return;
            }

            if (operation.Kind == InMemoryTransactionalBatchOperationKind.Patch)
            {
                var idToPatch = operation.Id!;
                var indexToPatch = FindEntityIndex(
                    entities,
                    idToPatch);

                if (indexToPatch < 0)
                {
                    throw TransactionalBatchError.EntityNotFound(
                        operationIndex,
                        idToPatch,
                        partitionKey,
                        typeof(TEntity).Name);
                }

                entities[indexToPatch] = BuildPatchedEntity(
                    entities[indexToPatch],
                    idToPatch,
                    partitionKey,
                    operation.PatchOperations!,
                    operation.PatchCondition,
                    operationIndex);
                return;
            }

            var entity = operation.Entity!;
            var id = ResolveIdValue(entity);
            var existingIndex = FindEntityIndex(
                entities,
                id);

            switch (operation.Kind)
            {
                case InMemoryTransactionalBatchOperationKind.Create:
                    if (existingIndex >= 0)
                    {
                        throw TransactionalBatchError.AlreadyExists(
                            operationIndex,
                            id);
                    }

                    entities.Add(Copy(entity, NextETag()));
                    break;

                case InMemoryTransactionalBatchOperationKind.Replace:
                    if (existingIndex < 0)
                    {
                        throw TransactionalBatchError.EntityNotFound(
                            operationIndex,
                            id,
                            partitionKey,
                            typeof(TEntity).Name);
                    }

                    if (!ETagMatches(
                            entity,
                            entities[existingIndex]))
                    {
                        throw TransactionalBatchError.ETagMismatch(
                            operationIndex,
                            id,
                            partitionKey);
                    }

                    // replaced in place, so an iteration of this partition keeps the insertion order
                    entities[existingIndex] = Copy(entity, NextETag());
                    break;

                case InMemoryTransactionalBatchOperationKind.Upsert:
                    var upsertedEntity = Copy(entity, NextETag());
                    if (existingIndex < 0)
                    {
                        entities.Add(upsertedEntity);
                    }
                    else
                    {
                        entities[existingIndex] = upsertedEntity;
                    }

                    break;
            }
        }

        /// <summary>
        ///     Returns a patched copy of the stored entity. The stored entity is left untouched, so
        ///     a patch that fails half way through - or a batch that fails after it - cannot leave
        ///     a partially patched document behind.
        /// </summary>
        /// <param name="storedEntity">The entity as it is stored</param>
        /// <param name="id">The id of the entity, for the error messages</param>
        /// <param name="partitionKey">The partition of the entity, for the error messages</param>
        /// <param name="operations">The operations to apply</param>
        /// <param name="condition">The compiled condition that has to hold, null when unconditional</param>
        /// <param name="batchOperationIndex">
        ///     The index of the operation inside the batch that carries the patch, null for a
        ///     standalone patch. Only the message differs, the error code does not.
        /// </param>
        private TEntity BuildPatchedEntity(
            TEntity storedEntity,
            string id,
            string partitionKey,
            IReadOnlyList<DatabasePatchOperation> operations,
            Func<TEntity, bool>? condition,
            int? batchOperationIndex)
        {
            // the condition is evaluated against the stored state, and nothing is written when it
            // does not hold - the check and the update are one act, like they are in Cosmos.
            // Against a copy of it: this provider accepts conditions Cosmos could not translate,
            // including ones calling a method, and evaluating a condition must not be able to
            // change what is stored
            if (condition != null && !condition(storedEntity.Clone()))
            {
                throw batchOperationIndex.HasValue
                    ? PatchError.ConditionNotMet(
                        batchOperationIndex.Value,
                        id,
                        partitionKey)
                    : PatchError.ConditionNotMet(
                        id,
                        partitionKey);
            }

            // a patch bumps the eTag, the same way a replace does
            var patchedEntity = Copy(storedEntity, NextETag());

            foreach (var operation in operations)
            {
                InMemoryPatchApplier.Apply(
                    patchedEntity,
                    operation,
                    id,
                    partitionKey);
            }

            // a Set can carry a reference-typed value the caller still holds on to; copied once
            // more so the store stays independent of it, like every other write path of this client
            return patchedEntity.Clone();
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

            return FindEntityIndex(
                entities,
                id);
        }

        private int FindEntityIndex(List<TEntity> entities, string id)
        {
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
            if (!ETagMatches(
                    entity,
                    existingEntity))
            {
                throw Error.PreconditionFailed(
                    "EtagMismatch",
                    $"The eTag of the entity with id {id} and partition key {partitionKey} does not match the version in the database");
            }
        }

        /// <summary>
        ///     Returns whether the eTag the entity carries still matches the stored one. An entity
        ///     without an eTag does not ask for a precondition, which mirrors Cosmos' behaviour for
        ///     a null IfMatchEtag.
        /// </summary>
        private bool ETagMatches(TEntity entity, TEntity existingEntity)
        {
            var eTag = ResolveETagValue(entity);

            if (string.IsNullOrEmpty(eTag))
            {
                return true;
            }

            return eTag == ResolveETagValue(existingEntity);
        }
    }
}
