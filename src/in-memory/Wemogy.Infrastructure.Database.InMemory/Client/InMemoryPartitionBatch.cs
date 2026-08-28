using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FastExpressionCompiler;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     In-memory implementation of a mixed-type partition batch. Each entity type keeps its own
    ///     static store, so a batch that mixes types has to write to several stores and still appear
    ///     atomic across all of them. It does so by opening a staging per participating store and
    ///     executing in two phases under the one process-wide lock: every operation is staged first,
    ///     and only once all of them staged without error is any store committed. A failure during
    ///     staging therefore leaves every store untouched, the same guarantee Cosmos DB gives.
    /// </summary>
    public class InMemoryPartitionBatch : DatabasePartitionBatchBase
    {
        /// <summary>
        ///     One participant per entity type the batch touches, in the order the types were first
        ///     seen. The order only decides which store opens its staging and commits first, neither
        ///     of which can fail.
        /// </summary>
        private readonly List<IParticipant> _participants = new List<IParticipant>();

        private readonly Dictionary<Type, IParticipant> _participantsByType = new Dictionary<Type, IParticipant>();

        /// <summary>
        ///     Stages one recorded operation against its own participant's staging. Held in the order
        ///     the operations were added - across types, not grouped by them - because that is the
        ///     order Cosmos DB reports a failure in: when two operations of different types would
        ///     both fail, the error has to name the first one the caller added, not the first store
        ///     that happens to be staged.
        /// </summary>
        private readonly List<Action> _stageOperations = new List<Action>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="InMemoryPartitionBatch"/> class.
        /// </summary>
        /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
        public InMemoryPartitionBatch(PartitionKeyValue partitionKey)
            : base(partitionKey)
        {
        }

        /// <inheritdoc />
        protected override void ApplyCreate<T>(T entity)
        {
            // the id is passed along because a create is the one operation that has to lose to a
            // document of *another* type holding it, see EnsureIdIsFreeInTheOtherStores
            Record(
                InMemoryTransactionalBatchOperation<T>.Create(entity),
                EntityMetadata<T>.ResolveId(entity));
        }

        /// <inheritdoc />
        protected override void ApplyReplace<T>(T entity)
        {
            Record(InMemoryTransactionalBatchOperation<T>.Replace(entity));
        }

        /// <inheritdoc />
        protected override void ApplyUpsert<T>(T entity)
        {
            Record(InMemoryTransactionalBatchOperation<T>.Upsert(entity));
        }

        /// <inheritdoc />
        protected override void ApplyDelete<T>(string id)
        {
            Record(InMemoryTransactionalBatchOperation<T>.Delete(id));
        }

        /// <inheritdoc />
        protected override void ApplyPatch<T>(
            string id,
            IReadOnlyList<DatabasePatchOperation> operations,
            Expression<Func<T, bool>>? condition)
        {
            // compiled when the operation is added, so the execution does not compile under the lock
            Record(
                InMemoryTransactionalBatchOperation<T>.Patch(
                    id,
                    operations,
                    condition?.CompileFast()));
        }

        /// <inheritdoc />
        protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // the whole batch runs under one lock, so no other write can interleave between staging
            // and committing, and the several stores appear to change at one instant
            lock (InMemoryDatabaseSync.Gate)
            {
                foreach (var participant in _participants)
                {
                    participant.BeginStage(PartitionKey);
                }

                // staged before anything is committed, so a failure - a conflict, a missing entity,
                // a failed patch condition - throws before any store is written, leaving every store
                // untouched
                foreach (var stageOperation in _stageOperations)
                {
                    stageOperation();
                }

                foreach (var participant in _participants)
                {
                    participant.Commit();
                }
            }

            return Task.CompletedTask;
        }

        private void Record<T>(InMemoryTransactionalBatchOperation<T> operation, string? idToClaim = null)
            where T : class
        {
            var participant = GetParticipant<T>();

            // captured now, read at execute time: the index is the one the base class is about to
            // assign this operation
            var operationIndex = OperationCount;
            _stageOperations.Add(() =>
            {
                if (idToClaim != null)
                {
                    EnsureIdIsFreeInTheOtherStores(
                        participant,
                        idToClaim,
                        operationIndex);
                }

                participant.Stage(
                    operation,
                    operationIndex);
            });
        }

        /// <summary>
        ///     Throws if another type taking part in the batch already holds the id in this
        ///     partition. An id is unique per logical partition of a *container*, not per entity
        ///     type, so Cosmos DB answers 409 for a create that collides with a document of a
        ///     different shape - while this provider keeps a store per type and would not notice.
        ///     <para>
        ///         Only the types the batch itself touches can be consulted: which other types share
        ///         the container is not something this provider knows, because it does not model
        ///         containers at all. A collision with a co-located type that takes no part in the
        ///         batch therefore still passes here and fails against Cosmos DB.
        ///     </para>
        /// </summary>
        private void EnsureIdIsFreeInTheOtherStores(IParticipant claimant, string id, int operationIndex)
        {
            foreach (var participant in _participants)
            {
                if (ReferenceEquals(
                        participant,
                        claimant))
                {
                    continue;
                }

                if (participant.ContainsId(id))
                {
                    throw TransactionalBatchError.AlreadyExists(
                        operationIndex,
                        id);
                }
            }
        }

        private Participant<T> GetParticipant<T>()
            where T : class
        {
            if (_participantsByType.TryGetValue(
                    typeof(T),
                    out var existing))
            {
                return (Participant<T>)existing;
            }

            var participant = new Participant<T>();
            _participantsByType.Add(
                typeof(T),
                participant);
            _participants.Add(participant);
            return participant;
        }

        /// <summary>
        ///     A participant of the batch, hiding the entity type so the batch can hold participants
        ///     of several types in one list and drive the phases that do not need the type.
        /// </summary>
        private interface IParticipant
        {
            void BeginStage(PartitionKeyValue partitionKey);

            bool ContainsId(string id);

            void Commit();
        }

        /// <summary>
        ///     The staging of one entity type, opened on a client of that type - which shares the one
        ///     static store of the type - so the mixed batch reuses the same validation a single-type
        ///     batch uses.
        /// </summary>
        /// <typeparam name="T">The entity type this participant writes</typeparam>
        private sealed class Participant<T> : IParticipant
            where T : class
        {
            private readonly InMemoryDatabaseClient<T> _client = new InMemoryDatabaseClient<T>();

            private object? _staging;

            public void BeginStage(PartitionKeyValue partitionKey)
            {
                _staging = _client.BeginStaging(partitionKey);
            }

            public bool ContainsId(string id)
            {
                return _client.StagingContainsId(
                    _staging!,
                    id);
            }

            public void Stage(InMemoryTransactionalBatchOperation<T> operation, int operationIndex)
            {
                _client.StageOperation(
                    _staging!,
                    operation,
                    operationIndex);
            }

            public void Commit()
            {
                _client.CommitStaging(_staging!);
            }
        }
    }
}
