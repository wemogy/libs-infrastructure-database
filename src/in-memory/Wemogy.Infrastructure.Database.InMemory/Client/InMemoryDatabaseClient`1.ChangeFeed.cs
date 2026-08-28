using System;
using System.Collections.Generic;
using System.Linq;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    public partial class InMemoryDatabaseClient<TEntity>
    {
        /// <summary>
        ///     Every write, in the order it was applied, waiting to be read by the running
        ///     processors. Static like the store itself, so a processor created by one client sees
        ///     the writes of every other client over the same entity type.
        ///     <para>
        ///         Trimmed down to what the slowest running processor still needs, and dropped
        ///         entirely while none is running - a store nobody reads the feed of must not grow a
        ///         log of everything that ever happened to it.
        ///     </para>
        /// </summary>
        private static readonly List<InMemoryChangeRecord<TEntity>> ChangeLog =
            new List<InMemoryChangeRecord<TEntity>>();

        /// <summary>
        ///     What each processor name has read up to, and whether it still owes a replay. Survives
        ///     a processor being stopped, so one restarted under the same name continues where it left
        ///     off rather than replaying or skipping - which is what the leases of Cosmos DB do.
        /// </summary>
        private static readonly Dictionary<string, InMemoryChangeFeedLease> Leases =
            new Dictionary<string, InMemoryChangeFeedLease>();

        private static readonly List<InMemoryChangeFeedProcessor<TEntity>> RunningProcessors =
            new List<InMemoryChangeFeedProcessor<TEntity>>();

        /// <summary>
        ///     Position of the last write applied to the store, counted across all partitions. Stands
        ///     in for the log sequence number Cosmos DB assigns.
        /// </summary>
        private static long _changeSequence;

        public IChangeFeedProcessor CreateChangeFeedProcessor(
            string processorName,
            ChangeFeedHandler<TEntity> onChanges,
            ChangeFeedProcessorOptions? options)
        {
            EnsureOptionsAreValid(processorName, options);

            return new InMemoryChangeFeedProcessor<TEntity>(
                this,
                processorName,
                onChanges,
                null,
                options);
        }

        public IChangeFeedProcessor CreateAllVersionsAndDeletesChangeFeedProcessor(
            string processorName,
            AllVersionsAndDeletesChangeFeedHandler<TEntity> onChanges,
            ChangeFeedProcessorOptions? options)
        {
            EnsureOptionsAreValid(processorName, options);

            if (options?.StartFromBeginning == true)
            {
                throw ChangeFeedError.StartFromBeginningNotSupported();
            }

            return new InMemoryChangeFeedProcessor<TEntity>(
                this,
                processorName,
                null,
                onChanges,
                options);
        }

        /// <summary>
        ///     Registers a processor as running and hands it the position to start reading at.
        /// </summary>
        /// <param name="processor">The processor being started</param>
        /// <param name="processorName">The name its checkpoint is filed under</param>
        /// <param name="startFromBeginning">
        ///     Whether a processor without a checkpoint replays the documents the store already holds
        /// </param>
        /// <param name="replay">
        ///     The documents to replay before reading the log, per partition, or <c>null</c> when
        ///     there is nothing to replay. Taken under the same lock as the start position, so a
        ///     write racing the start is either replayed or read from the log, never both or neither.
        /// </param>
        internal void RegisterProcessor(
            InMemoryChangeFeedProcessor<TEntity> processor,
            string processorName,
            bool startFromBeginning,
            out List<KeyValuePair<PartitionKeyValue, List<TEntity>>>? replay)
        {
            lock (Gate)
            {
                if (Leases.TryGetValue(
                        processorName,
                        out var lease))
                {
                    // the lease outranks the start position the options ask for, the same way an
                    // existing Cosmos lease does: restarting a processor must not replay what it
                    // handled. A replay it never finished is still owed, though
                    processor.Cursor = lease.Checkpoint;
                    replay = lease.ReplayPending ? SnapshotPartitions() : null;
                }
                else
                {
                    processor.Cursor = _changeSequence;
                    replay = startFromBeginning ? SnapshotPartitions() : null;

                    // the lease is written when the processor first starts, not when it first handles
                    // something - the way Cosmos creates its lease at start. Without it a processor
                    // that was stopped before anything happened would start at the end of the feed
                    // again and skip everything written while it was down, and the change log would
                    // drop those writes for lack of anyone to keep them for
                    Leases.Add(
                        processorName,
                        new InMemoryChangeFeedLease(processor.Cursor, startFromBeginning));
                }

                RunningProcessors.Add(processor);
            }
        }

        /// <summary>
        ///     Records that the replay a processor owed was handled, so a later start of the same name
        ///     carries on with the log instead of replaying the store again.
        /// </summary>
        internal void ReplayHandled(string processorName)
        {
            lock (Gate)
            {
                if (Leases.TryGetValue(
                        processorName,
                        out var lease))
                {
                    lease.ReplayPending = false;
                }
            }
        }

        internal void UnregisterProcessor(InMemoryChangeFeedProcessor<TEntity> processor)
        {
            lock (Gate)
            {
                RunningProcessors.Remove(processor);
                TrimChangeLog();
            }
        }

        /// <summary>
        ///     Returns the writes recorded after <paramref name="afterSequence"/>, and the position
        ///     that reads up to.
        /// </summary>
        internal List<InMemoryChangeRecord<TEntity>> ReadChanges(long afterSequence, out long head)
        {
            lock (Gate)
            {
                head = _changeSequence;
                return ChangeLog
                    .Where(record => record.Sequence > afterSequence)
                    .ToList();
            }
        }

        /// <summary>
        ///     Records that a processor handled everything up to <paramref name="sequence"/>.
        /// </summary>
        internal void Checkpoint(
            InMemoryChangeFeedProcessor<TEntity> processor,
            string processorName,
            long sequence)
        {
            lock (Gate)
            {
                processor.Cursor = sequence;

                if (Leases.TryGetValue(
                        processorName,
                        out var lease))
                {
                    lease.Checkpoint = sequence;
                }

                TrimChangeLog();
            }
        }

        /// <summary>
        ///     Returns the documents of the given ids that are still stored, in the given order.
        ///     <para>
        ///         What the latest version feed carries is the document as it is now, not as it was
        ///         when it was written - so a document written and rewritten between two reads
        ///         arrives once carrying the second write, and one written and then deleted does not
        ///         arrive at all.
        ///     </para>
        /// </summary>
        internal List<TEntity> ResolveCurrent(PartitionKeyValue partitionKey, IEnumerable<string> ids)
        {
            lock (Gate)
            {
                if (!Partitions.TryGetValue(
                        partitionKey,
                        out var entities))
                {
                    return new List<TEntity>();
                }

                var storedById = new Dictionary<string, TEntity>(entities.Count);
                foreach (var entity in entities)
                {
                    storedById[ResolveIdValue(entity)] = entity;
                }

                var current = new List<TEntity>();
                foreach (var id in ids)
                {
                    if (storedById.TryGetValue(
                            id,
                            out var storedEntity))
                    {
                        current.Add(storedEntity.Clone());
                    }
                }

                return current;
            }
        }

        /// <summary>
        ///     Drops the records that nothing can ask for any more: the ones every running processor
        ///     has already handled *and* every checkpoint has already passed.
        ///     <para>
        ///         The checkpoints have to be counted, not just the running processors - a stopped
        ///         processor is expected to pick the writes made while it was stopped up when it
        ///         starts again, so the writes after its checkpoint are exactly what it still needs.
        ///     </para>
        /// </summary>
        private static void TrimChangeLog()
        {
            if (!TryGetLowestReadPosition(out var lowestReadPosition))
            {
                ChangeLog.Clear();
                return;
            }

            ChangeLog.RemoveAll(record => record.Sequence <= lowestReadPosition);
        }

        /// <summary>
        ///     The position behind which no reader can ever look again, or <c>false</c> when there is
        ///     no reader at all - neither a running processor nor a checkpoint one could resume from.
        /// </summary>
        private static bool TryGetLowestReadPosition(out long lowestReadPosition)
        {
            if (RunningProcessors.Count == 0 && Leases.Count == 0)
            {
                lowestReadPosition = 0;
                return false;
            }

            lowestReadPosition = long.MaxValue;

            foreach (var processor in RunningProcessors)
            {
                lowestReadPosition = Math.Min(
                    lowestReadPosition,
                    processor.Cursor);
            }

            foreach (var lease in Leases.Values)
            {
                lowestReadPosition = Math.Min(
                    lowestReadPosition,
                    lease.Checkpoint);
            }

            return true;
        }

        /// <summary>
        ///     Forgets every change and every lease of this entity type, and moves the processors that
        ///     are running to the end of the feed.
        ///     <para>
        ///         The change log has to keep every write after the lowest lease, because a stopped
        ///         processor is entitled to resume from it - so a process that keeps creating
        ///         processors under new names keeps growing it. A suite that creates many can call
        ///         this in between; <c>DeleteAsync(x =&gt; true)</c> resets the documents, this resets
        ///         what the feed remembers about them.
        ///     </para>
        /// </summary>
        public static void ResetChangeFeed()
        {
            lock (Gate)
            {
                ChangeLog.Clear();
                Leases.Clear();

                foreach (var processor in RunningProcessors)
                {
                    processor.Cursor = _changeSequence;
                }
            }
        }

        private static List<KeyValuePair<PartitionKeyValue, List<TEntity>>> SnapshotPartitions()
        {
            return Partitions
                .Where(partition => partition.Value.Count > 0)
                .Select(partition => new KeyValuePair<PartitionKeyValue, List<TEntity>>(
                    partition.Key,
                    partition.Value.Select(entity => entity.Clone()).ToList()))
                .ToList();
        }

        private static void EnsureOptionsAreValid(string processorName, ChangeFeedProcessorOptions? options)
        {
            if (string.IsNullOrWhiteSpace(processorName))
            {
                throw ChangeFeedError.ProcessorNameIsEmpty();
            }

            if (options?.MaxItemsPerBatch <= 0)
            {
                throw ChangeFeedError.MaxItemsPerBatchIsNotPositive(options.MaxItemsPerBatch!.Value);
            }
        }

        /// <summary>
        ///     Appends a write to the change log. Has to be called while <see cref="Gate"/> is held,
        ///     so the order of the log is the order the writes were applied in.
        /// </summary>
        private void RecordChange(
            DatabaseChangeOperation operation,
            PartitionKeyValue partitionKey,
            string id,
            TEntity? current,
            TEntity? previous)
        {
            var sequence = ++_changeSequence;

            if (RunningProcessors.Count == 0 && Leases.Count == 0)
            {
                // nothing reads this store and nothing ever did, so there is nobody to keep the write
                // for. The sequence still advances, so a processor starting afterwards starts behind
                // these writes rather than replaying them
                ChangeLog.Clear();
                return;
            }

            ChangeLog.Add(
                new InMemoryChangeRecord<TEntity>(
                    sequence,
                    partitionKey,
                    id,
                    operation,
                    current?.Clone(),
                    previous?.Clone()));

            TrimChangeLog();
        }

        /// <summary>
        ///     A write a transactional batch made to its working copy, waiting for the batch to
        ///     succeed before it reaches the change log. The partition key is the one of the batch,
        ///     so it is not repeated here.
        /// </summary>
        private readonly struct PendingChange
        {
            public PendingChange(
                DatabaseChangeOperation operation,
                string id,
                TEntity? current,
                TEntity? previous)
            {
                Operation = operation;
                Id = id;
                Current = current;
                Previous = previous;
            }

            public DatabaseChangeOperation Operation { get; }

            public string Id { get; }

            public TEntity? Current { get; }

            public TEntity? Previous { get; }
        }

        /// <summary>
        ///     The result of staging a batch: a working copy of the partition and the changes it
        ///     produced, both waiting for the batch to commit. Kept as a type of its own so a
        ///     mixed-type batch can hold one per participating store and commit them together.
        ///     <para>
        ///         A class rather than a struct because it is handed out as <see cref="object"/> and
        ///         applying an operation mutates it: a struct would be copied out of the box on every
        ///         unboxing, and the copies would disagree about what has been staged.
        ///     </para>
        /// </summary>
        private sealed class BatchStaging
        {
            public BatchStaging(
                PartitionKeyValue partitionKey,
                List<TEntity> workingCopy,
                List<PendingChange> changes)
            {
                PartitionKey = partitionKey;
                WorkingCopy = workingCopy;
                Changes = changes;
            }

            public PartitionKeyValue PartitionKey { get; }

            public List<TEntity> WorkingCopy { get; }

            public List<PendingChange> Changes { get; }
        }
    }
}
