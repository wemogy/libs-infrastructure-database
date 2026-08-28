using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using FastExpressionCompiler;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     In-memory implementation of a mixed-type partition batch. Each entity type keeps its own
    ///     static store, so a batch that mixes types has to write to several stores and still appear
    ///     atomic across all of them. It does so by grouping the operations by type into
    ///     participants, and executing them in two phases under the one process-wide lock: every
    ///     participant is staged first, and only once all of them staged without error is any of
    ///     them committed. A failure during staging therefore leaves every store untouched, the same
    ///     guarantee Cosmos DB gives.
    /// </summary>
    public class InMemoryPartitionBatch : DatabasePartitionBatchBase
    {
        /// <summary>
        ///     One participant per entity type the batch touches, in the order the types were first
        ///     seen. The order only decides which store is staged and committed first, which does
        ///     not matter because the change feed is per type.
        /// </summary>
        private readonly List<IParticipant> _participants = new List<IParticipant>();

        private readonly Dictionary<Type, IParticipant> _participantsByType = new Dictionary<Type, IParticipant>();

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
            GetParticipant<T>().Add(
                OperationCount,
                InMemoryTransactionalBatchOperation<T>.Create(entity));
        }

        /// <inheritdoc />
        protected override void ApplyReplace<T>(T entity)
        {
            GetParticipant<T>().Add(
                OperationCount,
                InMemoryTransactionalBatchOperation<T>.Replace(entity));
        }

        /// <inheritdoc />
        protected override void ApplyUpsert<T>(T entity)
        {
            GetParticipant<T>().Add(
                OperationCount,
                InMemoryTransactionalBatchOperation<T>.Upsert(entity));
        }

        /// <inheritdoc />
        protected override void ApplyDelete<T>(string id)
        {
            GetParticipant<T>().Add(
                OperationCount,
                InMemoryTransactionalBatchOperation<T>.Delete(id));
        }

        /// <inheritdoc />
        protected override void ApplyPatch<T>(
            string id,
            IReadOnlyList<DatabasePatchOperation> operations,
            Expression<Func<T, bool>>? condition)
        {
            // compiled when the operation is added, so the execution does not compile under the lock
            GetParticipant<T>().Add(
                OperationCount,
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
                // staged first, so a failure - a conflict, a missing entity, a failed patch
                // condition - throws before any store is written, leaving every store untouched
                foreach (var participant in _participants)
                {
                    participant.Stage(PartitionKey);
                }

                foreach (var participant in _participants)
                {
                    participant.Commit();
                }
            }

            return Task.CompletedTask;
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
        ///     of several types in one list and drive their two phases without knowing the types.
        /// </summary>
        private interface IParticipant
        {
            void Stage(PartitionKeyValue partitionKey);

            void Commit();
        }

        /// <summary>
        ///     The operations of one entity type, applied through a client of that type - which
        ///     shares the one static store of the type - so the mixed batch reuses the same staging
        ///     and validation a single-type batch uses.
        /// </summary>
        /// <typeparam name="T">The entity type this participant writes</typeparam>
        private sealed class Participant<T> : IParticipant
            where T : class
        {
            private readonly InMemoryDatabaseClient<T> _client = new InMemoryDatabaseClient<T>();

            private readonly List<(int OperationIndex, InMemoryTransactionalBatchOperation<T> Operation)> _operations =
                new List<(int, InMemoryTransactionalBatchOperation<T>)>();

            private object? _staging;

            public void Add(int operationIndex, InMemoryTransactionalBatchOperation<T> operation)
            {
                _operations.Add((operationIndex, operation));
            }

            public void Stage(PartitionKeyValue partitionKey)
            {
                _staging = _client.StageBatch(
                    partitionKey,
                    _operations);
            }

            public void Commit()
            {
                _client.CommitStaging(_staging!);
            }
        }
    }
}
