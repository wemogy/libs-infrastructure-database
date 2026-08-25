using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Client;
using Wemogy.Infrastructure.Database.InMemory.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Client;

/// <summary>
///     The parts of the change feed the shared repository suite cannot cover against both providers:
///     the all-versions-and-deletes feed, which the Cosmos DB emulator does not serve, and the
///     redelivery and checkpointing behaviour, which would make a suite running against a real
///     service slow and flaky.
/// </summary>
[Collection("Sequential")]
public class InMemoryChangeFeedTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task ChangeFeed_ShouldDeliverTheChangesOfOnePartitionInWriteOrder()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var recorder = new Recorder();
        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            null);
        await processor.StartAsync();

        // Act
        await client.CreateAsync(NewEntity(partitionKey, "first"));
        await client.CreateAsync(NewEntity(partitionKey, "second"));
        await client.CreateAsync(NewEntity(partitionKey, "third"));

        // Assert
        await WaitUntilAsync(() => recorder.For(partitionKey).Count == 3);
        recorder.For(partitionKey)
            .Select(x => x.Key)
            .ShouldBe(new[] { "first", "second", "third" });
        recorder.Contexts.ShouldAllBe(context => context.RangeId == partitionKey);
    }

    [Fact]
    public async Task ChangeFeed_ShouldGroupTheChangesByPartition()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var firstPartitionKey = NewPartitionKey();
        var secondPartitionKey = NewPartitionKey();
        var recorder = new Recorder();
        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            null);
        await processor.StartAsync();

        // Act
        await client.CreateAsync(NewEntity(firstPartitionKey, "a"));
        await client.CreateAsync(NewEntity(secondPartitionKey, "b"));

        // Assert: a batch never mixes two ranges, because the order is only promised inside one
        await WaitUntilAsync(() =>
            recorder.For(firstPartitionKey).Count == 1 && recorder.For(secondPartitionKey).Count == 1);
        recorder.Batches.ShouldAllBe(batch => batch.Select(x => x.Tenant).Distinct().Count() == 1);
    }

    [Fact]
    public async Task ChangeFeed_ShouldDeliverARewrittenDocumentOnceWithItsLatestState()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var gate = new FirstCallGate();
        var recorder = new Recorder(gate.WaitAsync);
        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            null);
        await processor.StartAsync();
        await gate.HoldTheReaderAsync(() => client.CreateAsync(NewEntity(partitionKey, "gate")));

        // Act: both writes land while the reader is held inside the handler, so they are read together
        var entity = NewEntity(partitionKey, "rewritten");
        await client.CreateAsync(entity);
        entity.Name = "second";
        await client.ReplaceAsync(entity);
        gate.Release();

        // Assert: two writes to one document arrive as one change, carrying the second one
        await WaitUntilAsync(() => recorder.For(partitionKey).Any(x => x.Key == "rewritten"));
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        recorder.For(partitionKey).Count(x => x.Key == "rewritten").ShouldBe(1);
        recorder.For(partitionKey).Single(x => x.Key == "rewritten").Name.ShouldBe("second");
    }

    [Fact]
    public async Task ChangeFeed_ShouldNotDeliverHardDeletes()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var recorder = new Recorder();
        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            null);
        await processor.StartAsync();

        // Act
        await client.CreateAsync(NewEntity(partitionKey, "deleted"));
        await WaitUntilAsync(() => recorder.For(partitionKey).Count == 1);
        await client.DeleteAsync("deleted", partitionKey);
        await client.CreateAsync(NewEntity(partitionKey, "kept"));

        // Assert: the delete is invisible on the latest version feed, the write after it is not
        await WaitUntilAsync(() => recorder.For(partitionKey).Any(x => x.Key == "kept"));
        recorder.For(partitionKey).Count(x => x.Key == "deleted").ShouldBe(1);
    }

    [Fact]
    public async Task ChangeFeed_ShouldNotDeliverWritesMadeBeforeItStartedByDefault()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        await client.CreateAsync(NewEntity(partitionKey, "before"));

        var recorder = new Recorder();
        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            null);
        await processor.StartAsync();

        // Act: a write after the start, so the assertion waits for something rather than a timeout
        await client.CreateAsync(NewEntity(partitionKey, "after"));

        // Assert: a fresh processor starts at the end of the feed, not at the start of the container
        await WaitUntilAsync(() => recorder.For(partitionKey).Any(x => x.Key == "after"));
        recorder.For(partitionKey).ShouldNotContain(x => x.Key == "before");
    }

    [Fact]
    public async Task ChangeFeed_ShouldReplayTheStoredDocumentsWhenStartedFromBeginning()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        await client.CreateAsync(NewEntity(partitionKey, "before"));

        var recorder = new Recorder();
        var options = new ChangeFeedProcessorOptions { StartFromBeginning = true };

        // Act
        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            options);
        await processor.StartAsync();
        await client.CreateAsync(NewEntity(partitionKey, "after"));

        // Assert: the document that existed before the processor did is replayed, then the feed
        // continues without repeating it
        await WaitUntilAsync(() => recorder.For(partitionKey).Count == 2);
        recorder.For(partitionKey)
            .Select(x => x.Key)
            .ShouldBe(new[] { "before", "after" });
    }

    [Fact]
    public async Task ChangeFeed_ShouldRedeliverABatchTheHandlerFailedOn()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var attempts = 0;
        var errors = new List<Exception>();
        var recorder = new Recorder(() =>
        {
            // fails the first two times, so the batch has to arrive at least three times
            if (Interlocked.Increment(ref attempts) <= 2)
            {
                throw new InvalidOperationException("the projection is not ready");
            }

            return Task.CompletedTask;
        });

        var options = new ChangeFeedProcessorOptions
        {
            PollInterval = TimeSpan.FromMilliseconds(10),
            OnError = (context, exception) =>
            {
                lock (errors)
                {
                    errors.Add(exception);
                }

                return Task.CompletedTask;
            }
        };

        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            options);
        await processor.StartAsync();

        // Act
        await client.CreateAsync(NewEntity(partitionKey, "retried"));

        // Assert: nothing is checkpointed until the handler completed, so the change is not lost
        await WaitUntilAsync(() => recorder.For(partitionKey).Count > 0);
        attempts.ShouldBeGreaterThanOrEqualTo(3);
        lock (errors)
        {
            errors.ShouldAllBe(exception => exception is InvalidOperationException);
            errors.Count.ShouldBe(2);
        }
    }

    [Fact]
    public async Task ChangeFeed_ShouldResumeWhereTheSameProcessorNameStopped()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var processorName = NewProcessorName();
        var firstRun = new Recorder();
        var processor = client.CreateChangeFeedProcessor(
            processorName,
            firstRun.Handle,
            null);
        await processor.StartAsync();
        await client.CreateAsync(NewEntity(partitionKey, "before"));
        await WaitUntilAsync(() => firstRun.For(partitionKey).Count == 1);
        await processor.StopAsync();

        // Act: written while nothing is reading, then a processor of the same name starts again
        await client.CreateAsync(NewEntity(partitionKey, "while-stopped"));
        var secondRun = new Recorder();
        await using var resumedProcessor = client.CreateChangeFeedProcessor(
            processorName,
            secondRun.Handle,
            null);
        await resumedProcessor.StartAsync();

        // Assert: the write made while it was stopped is picked up, the one before it is not repeated
        await WaitUntilAsync(() => secondRun.For(partitionKey).Count == 1);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        secondRun.For(partitionKey).Single().Key.ShouldBe("while-stopped");
    }

    [Fact]
    public async Task ChangeFeed_ShouldPickUpWritesMadeWhileStoppedEvenIfItNeverHandledAnything()
    {
        // Arrange: started and stopped again without a single change passing through it
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var processorName = NewProcessorName();
        var firstRun = new Recorder();
        var processor = client.CreateChangeFeedProcessor(
            processorName,
            firstRun.Handle,
            null);
        await processor.StartAsync();
        await processor.StopAsync();

        // Act
        await client.CreateAsync(NewEntity(partitionKey, "while-stopped"));
        var secondRun = new Recorder();
        await using var resumedProcessor = client.CreateChangeFeedProcessor(
            processorName,
            secondRun.Handle,
            null);
        await resumedProcessor.StartAsync();

        // Assert: the checkpoint exists from the first start, so the write is not skipped as
        // "before the processor existed"
        await WaitUntilAsync(() => secondRun.For(partitionKey).Count == 1);
        secondRun.For(partitionKey).Single().Key.ShouldBe("while-stopped");
    }

    [Fact]
    public async Task ChangeFeed_ShouldSplitABatchAtTheConfiguredMaximum()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var gate = new FirstCallGate();
        var recorder = new Recorder(gate.WaitAsync);
        var options = new ChangeFeedProcessorOptions { MaxItemsPerBatch = 2 };
        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            options);
        await processor.StartAsync();
        await gate.HoldTheReaderAsync(() => client.CreateAsync(NewEntity(partitionKey, "gate")));

        // Act: written while the reader is held, so all five are read at once and have to be split
        for (var index = 0; index < 5; index++)
        {
            await client.CreateAsync(NewEntity(partitionKey, $"entity-{index}"));
        }

        gate.Release();

        // Assert
        await WaitUntilAsync(() => recorder.For(partitionKey).Count(x => x.Key != "gate") == 5);
        recorder.Batches.ShouldAllBe(batch => batch.Count <= 2);
    }

    [Fact]
    public async Task AllVersionsAndDeletesChangeFeed_ShouldReportEveryWriteSeparately()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var recorder = new AllVersionsRecorder();
        await using var processor = client.CreateAllVersionsAndDeletesChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            null);
        await processor.StartAsync();

        // Act
        var entity = NewEntity(partitionKey, "logged");
        await client.CreateAsync(entity);
        entity.Name = "renamed";
        await client.ReplaceAsync(entity);
        await client.DeleteAsync("logged", partitionKey);

        // Assert: unlike the latest version feed, the two writes and the delete each arrive
        await WaitUntilAsync(() => recorder.For(partitionKey).Count == 3);
        var changes = recorder.For(partitionKey);
        changes.Select(x => x.Operation)
            .ShouldBe(new[]
            {
                DatabaseChangeOperation.Create,
                DatabaseChangeOperation.Replace,
                DatabaseChangeOperation.Delete
            });

        changes[0].Current!.Name.ShouldBe("logged");
        changes[0].Previous.ShouldBeNull();
        changes[1].Current!.Name.ShouldBe("renamed");
        changes[1].Previous!.Name.ShouldBe("logged");

        // the deleted document is only still available as the previous version
        changes[2].Current.ShouldBeNull();
        changes[2].Previous!.Name.ShouldBe("renamed");
    }

    [Fact]
    public async Task ChangeFeed_ShouldDeliverTheWritesOfATransactionalBatchOnlyWhenItSucceeded()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var partitionKey = NewPartitionKey();
        var recorder = new AllVersionsRecorder();
        await using var processor = client.CreateAllVersionsAndDeletesChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            null);
        await processor.StartAsync();

        // Act: the second operation fails the batch, so neither operation is applied
        var failingBatch = client.CreateTransactionalBatch(partitionKey);
        failingBatch.Create(NewEntity(partitionKey, "rolled-back"));
        failingBatch.Delete("does-not-exist");
        await Should.ThrowAsync<Exception>(() => failingBatch.ExecuteAsync());

        var succeedingBatch = client.CreateTransactionalBatch(partitionKey);
        succeedingBatch.Create(NewEntity(partitionKey, "committed"));
        await succeedingBatch.ExecuteAsync();

        // Assert: a batch that left nothing behind in the store left nothing on the feed either
        await WaitUntilAsync(() => recorder.For(partitionKey).Count == 1);
        await Task.Delay(TimeSpan.FromMilliseconds(200));
        recorder.For(partitionKey).Single().Current!.Key.ShouldBe("committed");
    }

    [Fact]
    public async Task ChangeFeed_ShouldThrowWhenAProcessorIsStartedTwice()
    {
        // Arrange
        var client = new InMemoryDatabaseClient<KeyedEntity>();
        var recorder = new Recorder();
        await using var processor = client.CreateChangeFeedProcessor(
            NewProcessorName(),
            recorder.Handle,
            null);
        await processor.StartAsync();

        // Act & Assert
        (await Should.ThrowAsync<Wemogy.Core.Errors.Exceptions.UnexpectedErrorException>(
                () => processor.StartAsync()))
            .Code.ShouldBe("ChangeFeedProcessorAlreadyStarted");
    }

    private static string NewPartitionKey()
    {
        return Guid.NewGuid().ToString();
    }

    private static string NewProcessorName()
    {
        return $"test-{Guid.NewGuid():N}";
    }

    private static KeyedEntity NewEntity(string partitionKey, string id)
    {
        return new KeyedEntity
        {
            Key = id,
            Tenant = partitionKey,
            Name = id
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > Timeout)
            {
                throw new TimeoutException($"The change feed did not deliver the expected changes within {Timeout}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10));
        }
    }

    /// <summary>
    ///     Holds the reading loop inside the handler on its first invocation, so a test can write
    ///     while it is certain the processor is not reading. Without it, whether two writes are read
    ///     together depends on where the poll interval happens to fall.
    /// </summary>
    private class FirstCallGate
    {
        private readonly TaskCompletionSource<bool> _entered =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _released =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _hasEntered;

        /// <summary>
        ///     Triggers a first read with <paramref name="write"/> and returns once the handler is
        ///     inside the gate.
        /// </summary>
        public async Task HoldTheReaderAsync(Func<Task> write)
        {
            await write();
            await _entered.Task;
        }

        public void Release()
        {
            _released.TrySetResult(true);
        }

        public async Task WaitAsync()
        {
            if (Interlocked.Exchange(ref _hasEntered, 1) != 0)
            {
                return;
            }

            _entered.SetResult(true);
            await _released.Task;
        }
    }

    private class Recorder
    {
        private readonly object _gate = new object();
        private readonly List<KeyedEntity> _changes = new List<KeyedEntity>();
        private readonly List<List<KeyedEntity>> _batches = new List<List<KeyedEntity>>();
        private readonly List<ChangeFeedContext> _contexts = new List<ChangeFeedContext>();
        private readonly Func<Task>? _onHandle;

        public Recorder(Func<Task>? onHandle = null)
        {
            _onHandle = onHandle;
        }

        public List<List<KeyedEntity>> Batches
        {
            get
            {
                lock (_gate)
                {
                    return _batches.ToList();
                }
            }
        }

        public List<ChangeFeedContext> Contexts
        {
            get
            {
                lock (_gate)
                {
                    return _contexts.ToList();
                }
            }
        }

        public async Task Handle(
            IReadOnlyCollection<KeyedEntity> changes,
            ChangeFeedContext context,
            CancellationToken cancellationToken)
        {
            if (_onHandle != null)
            {
                // awaited before anything is recorded, so a handler that fails records nothing
                await _onHandle();
            }

            lock (_gate)
            {
                _changes.AddRange(changes);
                _batches.Add(changes.ToList());
                _contexts.Add(context);
            }
        }

        public List<KeyedEntity> For(string partitionKey)
        {
            lock (_gate)
            {
                return _changes
                    .Where(change => change.Tenant == partitionKey)
                    .ToList();
            }
        }
    }

    private class AllVersionsRecorder
    {
        private readonly object _gate = new object();
        private readonly List<DatabaseChange<KeyedEntity>> _changes = new List<DatabaseChange<KeyedEntity>>();

        public Task Handle(
            IReadOnlyCollection<DatabaseChange<KeyedEntity>> changes,
            ChangeFeedContext context,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _changes.AddRange(changes);
            }

            return Task.CompletedTask;
        }

        public List<DatabaseChange<KeyedEntity>> For(string partitionKey)
        {
            lock (_gate)
            {
                return _changes
                    .Where(change => (change.Current ?? change.Previous)!.Tenant == partitionKey)
                    .ToList();
            }
        }
    }
}
