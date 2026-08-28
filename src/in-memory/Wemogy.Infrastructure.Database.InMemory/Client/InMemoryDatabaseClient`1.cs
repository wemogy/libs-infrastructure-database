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
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Extensions;
using Wemogy.Infrastructure.Database.InMemory.Query;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    public partial class InMemoryDatabaseClient<TEntity> : DatabaseClientBase<TEntity>, IDatabaseClient<TEntity>
        where TEntity : class
    {
        /// <summary>
        ///     The store is static, so every client, factory and repository for this entity type
        ///     shares one database - the in-memory provider stands in for a single database, and two
        ///     repositories over the same entity have to see the same data. Because the type is
        ///     generic, each closed generic type gets its own store.
        /// </summary>
        private static readonly Dictionary<PartitionKeyValue, List<TEntity>> Partitions =
            new Dictionary<PartitionKeyValue, List<TEntity>>();

        /// <summary>
        ///     Guards <see cref="Partitions"/> and every entity list inside it. Clients are
        ///     typically registered as singletons, so concurrent requests would otherwise corrupt
        ///     the dictionaries. The lock is shared across every entity type (see
        ///     <see cref="InMemoryDatabaseSync"/>), so a mixed-type partition batch that writes to
        ///     several stores at once can hold one lock for all of them.
        /// </summary>
        private static readonly object Gate = InMemoryDatabaseSync.Gate;

        public Task<TEntity> GetAsync(string id, PartitionKeyValue partitionKey, CancellationToken cancellationToken)
        {
            EnsurePartitionKeyDepth(partitionKey);

            lock (Gate)
            {
                var entity = FindEntity(
                    partitionKey,
                    id);

                if (entity == null)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey.ToString(),
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
            EnsureFixedPointValuesAreValid(entity);
            var id = ResolveIdValue(entity);
            var partitionKeyValue = ResolvePartitionKey(entity);

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
                var createdEntity = Copy(entity, eTag);
                GetOrCreatePartition(partitionKeyValue).Add(createdEntity);
                RecordChange(
                    DatabaseChangeOperation.Create,
                    partitionKeyValue,
                    id,
                    createdEntity,
                    null);

                return Task.FromResult(Copy(entity, eTag));
            }
        }

        public Task<TEntity> ReplaceAsync(TEntity entity)
        {
            EnsureFixedPointValuesAreValid(entity);
            var id = ResolveIdValue(entity);
            var partitionKeyValue = ResolvePartitionKey(entity);

            lock (Gate)
            {
                var index = FindEntityIndex(
                    partitionKeyValue,
                    id);

                if (index < 0)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKeyValue.ToString(),
                        hint: typeof(TEntity).Name);
                }

                var entities = Partitions[partitionKeyValue];

                EnsureETagMatches(
                    entity,
                    entities[index],
                    id,
                    partitionKeyValue);

                var eTag = NextETag();
                var previousEntity = entities[index];
                var replacedEntity = Copy(entity, eTag);

                // replaced in place, so an iteration of this partition keeps the insertion order
                entities[index] = replacedEntity;
                RecordChange(
                    DatabaseChangeOperation.Replace,
                    partitionKeyValue,
                    id,
                    replacedEntity,
                    previousEntity);

                return Task.FromResult(Copy(entity, eTag));
            }
        }

        public Task<TEntity> UpsertAsync(TEntity entity)
        {
            return UpsertAsync(
                entity,
                ResolvePartitionKey(entity));
        }

        public Task<TEntity> UpsertAsync(TEntity entity, PartitionKeyValue partitionKey)
        {
            EnsureFixedPointValuesAreValid(entity);
            EnsurePartitionKeyDepth(partitionKey);

            var id = ResolveIdValue(entity);

            lock (Gate)
            {
                var entities = GetOrCreatePartition(partitionKey);
                var index = entities.FindIndex(x => ResolveIdValue(x) == id);
                var eTag = NextETag();

                var upsertedEntity = Copy(entity, eTag);

                // an upsert carries no precondition, mirroring a Cosmos upsert without IfMatch
                if (index < 0)
                {
                    entities.Add(upsertedEntity);
                    RecordChange(
                        DatabaseChangeOperation.Create,
                        partitionKey,
                        id,
                        upsertedEntity,
                        null);
                }
                else
                {
                    var previousEntity = entities[index];
                    entities[index] = upsertedEntity;
                    RecordChange(
                        DatabaseChangeOperation.Replace,
                        partitionKey,
                        id,
                        upsertedEntity,
                        previousEntity);
                }

                return Task.FromResult(Copy(entity, eTag));
            }
        }

        public Task DeleteAsync(string id, PartitionKeyValue partitionKey)
        {
            EnsurePartitionKeyDepth(partitionKey);

            lock (Gate)
            {
                var entity = FindEntity(
                    partitionKey,
                    id);

                if (entity == null)
                {
                    throw DatabaseError.EntityNotFound(
                        id,
                        partitionKey.ToString(),
                        hint: typeof(TEntity).Name);
                }

                Partitions[partitionKey].Remove(entity);
                RecordChange(
                    DatabaseChangeOperation.Delete,
                    partitionKey,
                    id,
                    null,
                    entity);
                return Task.CompletedTask;
            }
        }

        public Task DeleteAsync(Expression<Func<TEntity, bool>> predicate)
        {
            var compiledPredicate = predicate.CompileFast();

            lock (Gate)
            {
                foreach (var partition in Partitions)
                {
                    var deletedEntities = partition.Value.Where(compiledPredicate).ToList();
                    foreach (var deletedEntity in deletedEntities)
                    {
                        partition.Value.Remove(deletedEntity);
                        RecordChange(
                            DatabaseChangeOperation.Delete,
                            partition.Key,
                            ResolveIdValue(deletedEntity),
                            null,
                            deletedEntity);
                    }
                }

                return Task.CompletedTask;
            }
        }

        public IDatabaseTransactionalBatch<TEntity> CreateTransactionalBatch(PartitionKeyValue partitionKey)
        {
            EnsurePartitionKeyDepth(partitionKey);

            return new InMemoryTransactionalBatch<TEntity>(
                this,
                partitionKey,
                ResolvePartitionKey);
        }

        public IDatabasePartitionBatch CreatePartitionBatch(PartitionKeyValue partitionKey)
        {
            EnsurePartitionKeyDepth(partitionKey);

            return new InMemoryPartitionBatch(partitionKey);
        }

        public Task<TEntity> PatchAsync(
            string id,
            PartitionKeyValue partitionKey,
            Action<IPatchOperations<TEntity>> operations,
            Expression<Func<TEntity, bool>>? condition,
            CancellationToken cancellationToken)
        {
            try
            {
                EnsurePartitionKeyDepth(partitionKey);

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
                            partitionKey.ToString(),
                            hint: typeof(TEntity).Name);
                    }

                    var entities = Partitions[partitionKey];
                    var previousEntity = entities[index];
                    var patchedEntity = BuildPatchedEntity(
                        previousEntity,
                        id,
                        partitionKey,
                        patchOperations,
                        compiledCondition,
                        null);

                    // replaced in place, so an iteration of this partition keeps the insertion order
                    entities[index] = patchedEntity;

                    // a patch is a write like any other on the feed, and it carries the whole
                    // document rather than the fields it touched - which is what lets a projection
                    // treat a patched and a replaced document the same way
                    RecordChange(
                        DatabaseChangeOperation.Replace,
                        partitionKey,
                        id,
                        patchedEntity,
                        previousEntity);

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
            PartitionKeyValue partitionKey,
            IReadOnlyList<InMemoryTransactionalBatchOperation<TEntity>> operations)
        {
            var indexedOperations = new List<(int OperationIndex, InMemoryTransactionalBatchOperation<TEntity> Operation)>(operations.Count);
            for (var index = 0; index < operations.Count; index++)
            {
                indexedOperations.Add((index, operations[index]));
            }

            lock (Gate)
            {
                var staging = StageBatch(
                    partitionKey,
                    indexedOperations);
                CommitStaging(staging);
            }
        }

        /// <summary>
        ///     Validates the operations of a batch against a working copy of the partition without
        ///     touching the store, and returns the staged result to be committed later. This is the
        ///     first phase of a two-phase execution: a mixed-type batch stages every participating
        ///     store before it commits any of them, so a failure in one store leaves every store
        ///     untouched.
        ///     <para>
        ///         Has to be called while <see cref="Gate"/> is held, so the state it validates
        ///         against does not change before the matching <see cref="CommitStaging"/> writes it.
        ///     </para>
        /// </summary>
        /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
        /// <param name="operations">The recorded operations paired with the index to report a failure at</param>
        /// <returns>An opaque staging result to hand to <see cref="CommitStaging"/></returns>
        internal object StageBatch(
            PartitionKeyValue partitionKey,
            IReadOnlyList<(int OperationIndex, InMemoryTransactionalBatchOperation<TEntity> Operation)> operations)
        {
            var workingCopy = Partitions.TryGetValue(
                partitionKey,
                out var entities)
                ? new List<TEntity>(entities)
                : new List<TEntity>();

            // collected rather than recorded straight away, so a batch that fails half way
            // through leaves nothing on the change feed either - the same reason the entities
            // are applied to a working copy
            var changes = new List<PendingChange>();

            foreach (var (operationIndex, operation) in operations)
            {
                ApplyBatchOperation(
                    workingCopy,
                    operation,
                    operationIndex,
                    partitionKey,
                    changes);
            }

            return new BatchStaging(
                partitionKey,
                workingCopy,
                changes);
        }

        /// <summary>
        ///     Writes a staged batch to the store and appends its changes to the feed. This is the
        ///     second phase of a two-phase execution and only runs once every participating store
        ///     staged without error, so it cannot fail on validation.
        ///     <para>
        ///         Has to be called while <see cref="Gate"/> is held, and with the value a matching
        ///         <see cref="StageBatch"/> returned.
        ///     </para>
        /// </summary>
        /// <param name="staging">The staging result of <see cref="StageBatch"/></param>
        internal void CommitStaging(object staging)
        {
            var batchStaging = (BatchStaging)staging;

            Partitions[batchStaging.PartitionKey] = batchStaging.WorkingCopy;

            foreach (var change in batchStaging.Changes)
            {
                RecordChange(
                    change.Operation,
                    batchStaging.PartitionKey,
                    change.Id,
                    change.Current,
                    change.Previous);
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

        private static List<TEntity> GetOrCreatePartition(PartitionKeyValue partitionKey)
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
            PartitionKeyValue partitionKey,
            List<PendingChange> changes)
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
                        partitionKey.ToString(),
                        typeof(TEntity).Name);
                }

                var deletedEntity = entities[indexToDelete];
                entities.RemoveAt(indexToDelete);
                changes.Add(
                    new PendingChange(
                        DatabaseChangeOperation.Delete,
                        idToDelete,
                        null,
                        deletedEntity));
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
                        partitionKey.ToString(),
                        typeof(TEntity).Name);
                }

                var entityBeforePatch = entities[indexToPatch];
                var patchedEntity = BuildPatchedEntity(
                    entityBeforePatch,
                    idToPatch,
                    partitionKey,
                    operation.PatchOperations!,
                    operation.PatchCondition,
                    operationIndex);
                entities[indexToPatch] = patchedEntity;
                changes.Add(
                    new PendingChange(
                        DatabaseChangeOperation.Replace,
                        idToPatch,
                        patchedEntity,
                        entityBeforePatch));
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

                    var createdEntity = Copy(entity, NextETag());
                    entities.Add(createdEntity);
                    changes.Add(
                        new PendingChange(
                            DatabaseChangeOperation.Create,
                            id,
                            createdEntity,
                            null));
                    break;

                case InMemoryTransactionalBatchOperationKind.Replace:
                    if (existingIndex < 0)
                    {
                        throw TransactionalBatchError.EntityNotFound(
                            operationIndex,
                            id,
                            partitionKey.ToString(),
                            typeof(TEntity).Name);
                    }

                    if (!ETagMatches(
                            entity,
                            entities[existingIndex]))
                    {
                        throw TransactionalBatchError.ETagMismatch(
                            operationIndex,
                            id,
                            partitionKey.ToString());
                    }

                    var entityBeforeReplace = entities[existingIndex];
                    var replacedEntity = Copy(entity, NextETag());

                    // replaced in place, so an iteration of this partition keeps the insertion order
                    entities[existingIndex] = replacedEntity;
                    changes.Add(
                        new PendingChange(
                            DatabaseChangeOperation.Replace,
                            id,
                            replacedEntity,
                            entityBeforeReplace));
                    break;

                case InMemoryTransactionalBatchOperationKind.Upsert:
                    var upsertedEntity = Copy(entity, NextETag());
                    if (existingIndex < 0)
                    {
                        entities.Add(upsertedEntity);
                        changes.Add(
                            new PendingChange(
                                DatabaseChangeOperation.Create,
                                id,
                                upsertedEntity,
                                null));
                    }
                    else
                    {
                        var entityBeforeUpsert = entities[existingIndex];
                        entities[existingIndex] = upsertedEntity;
                        changes.Add(
                            new PendingChange(
                                DatabaseChangeOperation.Replace,
                                id,
                                upsertedEntity,
                                entityBeforeUpsert));
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
            PartitionKeyValue partitionKey,
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
                        partitionKey.ToString())
                    : PatchError.ConditionNotMet(
                        id,
                        partitionKey.ToString());
            }

            // a patch bumps the eTag, the same way a replace does
            var patchedEntity = Copy(storedEntity, NextETag());

            foreach (var operation in operations)
            {
                InMemoryPatchApplier.Apply(
                    patchedEntity,
                    operation,
                    id,
                    partitionKey.ToString());
            }

            // a Set can carry a reference-typed value the caller still holds on to; copied once
            // more so the store stays independent of it, like every other write path of this client
            return patchedEntity.Clone();
        }

        private TEntity? FindEntity(PartitionKeyValue partitionKey, string id)
        {
            var index = FindEntityIndex(
                partitionKey,
                id);
            return index < 0 ? null : Partitions[partitionKey][index];
        }

        private int FindEntityIndex(PartitionKeyValue partitionKey, string id)
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

        private void EnsureETagMatches(TEntity entity, TEntity existingEntity, string id, PartitionKeyValue partitionKey)
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
