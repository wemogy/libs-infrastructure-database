using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     Reads the change log of the in-memory store in a loop and hands the writes to a handler,
    ///     with the semantics the Cosmos DB change feed processor has:
    ///     <list type="bullet">
    ///         <item>a batch is only checkpointed once the handler completed, so a throwing handler
    ///             sees its batch again rather than losing it</item>
    ///         <item>the checkpoint is kept per processor name, so a restarted processor continues
    ///             where the previous one stopped</item>
    ///         <item>changes are handed over grouped by range, and one logical partition is one range
    ///             here - narrower than the physical range of a real container, but never promising
    ///             an order Cosmos DB would not keep</item>
    ///     </list>
    ///     <para>
    ///         What it does *not* model is lease contention: two processors running under the same
    ///         name each see every change instead of splitting the ranges between them. Nothing in a
    ///         test needs the split, and modelling it would make a single-processor test depend on
    ///         which instance won a lease.
    ///     </para>
    /// </summary>
    /// <typeparam name="TEntity">The entity type of the repository the feed reads from</typeparam>
    internal class InMemoryChangeFeedProcessor<TEntity> : IChangeFeedProcessor
        where TEntity : class
    {
        /// <summary>
        ///     Short enough that a test asserting on a write does not wait for it, and long enough
        ///     that an idle processor does not spin. Overridden by
        ///     <see cref="ChangeFeedProcessorOptions.PollInterval"/>.
        /// </summary>
        private static readonly TimeSpan DefaultPollInterval = TimeSpan.FromMilliseconds(25);

        private readonly InMemoryDatabaseClient<TEntity> _client;
        private readonly string _processorName;
        private readonly ChangeFeedHandler<TEntity>? _onChanges;
        private readonly AllVersionsAndDeletesChangeFeedHandler<TEntity>? _onAllVersionsAndDeletesChanges;
        private readonly ChangeFeedProcessorOptions? _options;

        /// <summary>
        ///     Guards start and stop against each other. The reading loop itself needs no lock: only
        ///     it touches <see cref="Cursor"/> outside the store's own lock.
        /// </summary>
        private readonly object _gate = new object();

        private CancellationTokenSource? _cancellationTokenSource;
        private Task? _readLoop;

        /// <summary>
        ///     The documents to replay before the log is read, taken when the processor started.
        ///     Cleared once they were handed over, and kept until then so a handler that threw during
        ///     the replay gets them again.
        /// </summary>
        private List<KeyValuePair<string, List<TEntity>>>? _replay;

        public InMemoryChangeFeedProcessor(
            InMemoryDatabaseClient<TEntity> client,
            string processorName,
            ChangeFeedHandler<TEntity>? onChanges,
            AllVersionsAndDeletesChangeFeedHandler<TEntity>? onAllVersionsAndDeletesChanges,
            ChangeFeedProcessorOptions? options)
        {
            _client = client;
            _processorName = processorName;
            _onChanges = onChanges;
            _onAllVersionsAndDeletesChanges = onAllVersionsAndDeletesChanges;
            _options = options;
        }

        /// <summary>
        ///     The position of the last write this processor handled. Written by the store under its
        ///     own lock, which is also where it is read to decide what the change log still needs to
        ///     keep.
        /// </summary>
        internal long Cursor { get; set; }

        public Task StartAsync()
        {
            lock (_gate)
            {
                if (_readLoop != null)
                {
                    throw ChangeFeedError.AlreadyStarted(_processorName);
                }

                _client.RegisterProcessor(
                    this,
                    _processorName,
                    _options?.StartFromBeginning == true,
                    out _replay);

                _cancellationTokenSource = new CancellationTokenSource();
                var cancellationToken = _cancellationTokenSource.Token;
                _readLoop = Task.Run(
                    () => ReadAsync(cancellationToken),
                    cancellationToken);
            }

            return Task.CompletedTask;
        }

        public async Task StopAsync()
        {
            Task readLoop;
            CancellationTokenSource cancellationTokenSource;

            lock (_gate)
            {
                if (_readLoop == null)
                {
                    return;
                }

                readLoop = _readLoop;
                cancellationTokenSource = _cancellationTokenSource!;
                _readLoop = null;
                _cancellationTokenSource = null;
            }

            cancellationTokenSource.Cancel();

            try
            {
                await readLoop;
            }
            catch (OperationCanceledException)
            {
                // stopping is how the loop ends, so the cancellation it throws is not a failure
            }
            finally
            {
                cancellationTokenSource.Dispose();
                _client.UnregisterProcessor(this);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }

        /// <summary>
        ///     Groups the writes by the range they belong to - one logical partition per range here -
        ///     keeping both the order of the writes inside a range and the order the ranges were
        ///     first written to.
        /// </summary>
        private static List<KeyValuePair<string, List<InMemoryChangeRecord<TEntity>>>> GroupByPartition(
            List<InMemoryChangeRecord<TEntity>> records)
        {
            var partitions = new List<KeyValuePair<string, List<InMemoryChangeRecord<TEntity>>>>();
            var recordsByPartition = new Dictionary<string, List<InMemoryChangeRecord<TEntity>>>();

            foreach (var record in records)
            {
                if (!recordsByPartition.TryGetValue(
                        record.PartitionKey,
                        out var partitionRecords))
                {
                    partitionRecords = new List<InMemoryChangeRecord<TEntity>>();
                    recordsByPartition.Add(
                        record.PartitionKey,
                        partitionRecords);
                    partitions.Add(
                        new KeyValuePair<string, List<InMemoryChangeRecord<TEntity>>>(
                            record.PartitionKey,
                            partitionRecords));
                }

                partitionRecords.Add(record);
            }

            return partitions;
        }

        private static DatabaseChange<TEntity> ToDatabaseChange(InMemoryChangeRecord<TEntity> record)
        {
            // copied on the way out as well as on the way in, so a handler holding on to a change -
            // or mutating it - cannot reach the log a redelivery would read again
            return new DatabaseChange<TEntity>(
                record.Operation,
                record.Current?.Clone(),
                record.Previous?.Clone());
        }

        private async Task ReadAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await ReadOnceAsync(cancellationToken);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // reading the log itself failed rather than a handler, so there is no range to
                    // name. Not checkpointed either, so the next pass reads the same writes again
                    await NotifyErrorAsync(
                        new ChangeFeedContext(string.Empty),
                        exception);
                }

                try
                {
                    await Task.Delay(
                        _options?.PollInterval ?? DefaultPollInterval,
                        cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
            }
        }

        /// <summary>
        ///     Hands over everything written since the last checkpoint, and moves the checkpoint
        ///     forward once all of it was handled.
        /// </summary>
        private async Task ReadOnceAsync(CancellationToken cancellationToken)
        {
            if (_replay != null)
            {
                foreach (var partition in _replay)
                {
                    if (!await InvokeLatestVersionHandlerAsync(
                            partition.Key,
                            partition.Value,
                            cancellationToken))
                    {
                        // the replay stays owed, both here and on the lease, so a failure or a
                        // shutdown half way through does not lose the rest of the snapshot
                        return;
                    }
                }

                _client.ReplayHandled(_processorName);
                _replay = null;
            }

            var records = _client.ReadChanges(
                Cursor,
                out var head);

            if (records.Count == 0)
            {
                return;
            }

            foreach (var partition in GroupByPartition(records))
            {
                var handled = _onChanges != null
                    ? await InvokeLatestVersionHandlerAsync(
                        partition.Key,
                        ResolveLatestVersions(
                            partition.Key,
                            partition.Value),
                        cancellationToken)
                    : await InvokeAllVersionsAndDeletesHandlerAsync(
                        partition.Key,
                        partition.Value,
                        cancellationToken);

                if (!handled)
                {
                    // the whole pass is retried, including the partitions that already succeeded -
                    // a handler is called at least once per change, not exactly once
                    return;
                }
            }

            _client.Checkpoint(
                this,
                _processorName,
                head);
        }

        /// <summary>
        ///     Resolves the writes of one partition into the documents the latest version feed
        ///     carries: the current state of each document that changed, once, in the order of the
        ///     last write that touched it. Documents that are gone by now are left out, deletes among
        ///     the writes with them.
        /// </summary>
        private List<TEntity> ResolveLatestVersions(
            string partitionKey,
            List<InMemoryChangeRecord<TEntity>> records)
        {
            var lastWriteIndexById = new Dictionary<string, int>();
            for (var index = 0; index < records.Count; index++)
            {
                lastWriteIndexById[records[index].Id] = index;
            }

            var ids = lastWriteIndexById
                .OrderBy(entry => entry.Value)
                .Select(entry => entry.Key);

            return _client.ResolveCurrent(
                partitionKey,
                ids);
        }

        /// <summary>
        ///     Invokes the handler for one range, in batches of at most
        ///     <see cref="ChangeFeedProcessorOptions.MaxItemsPerBatch"/>.
        /// </summary>
        /// <returns>Whether every batch was handled</returns>
        private async Task<bool> InvokeLatestVersionHandlerAsync(
            string partitionKey,
            List<TEntity> changes,
            CancellationToken cancellationToken)
        {
            var context = new ChangeFeedContext(partitionKey);

            foreach (var batch in Batch(changes))
            {
                try
                {
                    await _onChanges!(
                        batch,
                        context,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    await NotifyHandlerErrorAsync(
                        context,
                        exception,
                        cancellationToken);
                    return false;
                }
            }

            return true;
        }

        private async Task<bool> InvokeAllVersionsAndDeletesHandlerAsync(
            string partitionKey,
            List<InMemoryChangeRecord<TEntity>> records,
            CancellationToken cancellationToken)
        {
            var context = new ChangeFeedContext(partitionKey);
            var changes = records.Select(ToDatabaseChange).ToList();

            foreach (var batch in Batch(changes))
            {
                try
                {
                    await _onAllVersionsAndDeletesChanges!(
                        batch,
                        context,
                        cancellationToken);
                }
                catch (Exception exception)
                {
                    await NotifyHandlerErrorAsync(
                        context,
                        exception,
                        cancellationToken);
                    return false;
                }
            }

            return true;
        }

        private IEnumerable<IReadOnlyCollection<TChange>> Batch<TChange>(List<TChange> changes)
        {
            // a non-positive value is rejected when the processor is created, so it cannot mean
            // "unlimited" here the way it would if it were only defaulted away
            var maxItems = _options?.MaxItemsPerBatch;

            if (maxItems == null || changes.Count <= maxItems.Value)
            {
                if (changes.Count > 0)
                {
                    yield return changes;
                }

                yield break;
            }

            for (var offset = 0; offset < changes.Count; offset += maxItems.Value)
            {
                yield return changes
                    .Skip(offset)
                    .Take(maxItems.Value)
                    .ToList();
            }
        }

        private async Task NotifyHandlerErrorAsync(
            ChangeFeedContext context,
            Exception exception,
            CancellationToken cancellationToken)
        {
            if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
            {
                // the processor is being stopped, which is not something to report
                return;
            }

            await NotifyErrorAsync(
                context,
                exception);
        }

        private async Task NotifyErrorAsync(ChangeFeedContext context, Exception exception)
        {
            var onError = _options?.OnError;

            if (onError == null)
            {
                return;
            }

            try
            {
                await onError(
                    context,
                    exception);
            }
            catch
            {
                // an error handler that throws must not take the processor down with it - there
                // would be nowhere left to report that either
            }
        }
    }
}
