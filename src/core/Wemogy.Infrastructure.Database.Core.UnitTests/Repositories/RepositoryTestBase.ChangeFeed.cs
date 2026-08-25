using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

public partial class RepositoryTestBase
{
    /// <summary>
    ///     How long a test waits for a write to reach the handler. Generous, because a real change
    ///     feed processor has to acquire its leases before it reads anything - the tests do not wait
    ///     this long unless something is wrong, they poll.
    /// </summary>
    private static readonly TimeSpan ChangeFeedTimeout = TimeSpan.FromSeconds(60);

    /// <summary>
    ///     Runs before a test starts a processor, so a provider suite can set up the infrastructure
    ///     its change feed needs. The Cosmos DB suite creates the lease container here, which the
    ///     library deliberately does not create itself.
    /// </summary>
    protected virtual Task PrepareChangeFeedAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task ChangeFeedProcessor_ShouldObserveAWriteWithTheStateItLeftBehind()
    {
        // Arrange
        await PrepareChangeFeedAsync();
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var observedUsers = new ChangeFeedRecorder();
        await using var processor = MicrosoftUserRepository.CreateChangeFeedProcessor(
            NewProcessorName(),
            observedUsers.Handle,
            NewProcessorOptions());
        await processor.StartAsync();

        // Act
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        user.Firstname = "Renamed";
        await MicrosoftUserRepository.ReplaceAsync(user);

        // Assert: the change carries the document as it is now, not as the first write left it
        await WaitUntilAsync(() => observedUsers.For(partitionKey).Any(x => x.Firstname == "Renamed"));
        var observedUser = observedUsers.For(partitionKey).Last(x => x.Id == user.Id);
        observedUser.Firstname.ShouldBe("Renamed");
        observedUser.Lastname.ShouldBe(user.Lastname);
        observedUsers.Contexts.ShouldAllBe(context => context.RangeId.Length > 0);
    }

    [Fact]
    public async Task ChangeFeedProcessor_ShouldCarryTheWholeDocumentWhenTheWriteWasAPatch()
    {
        // Arrange
        await PrepareChangeFeedAsync();
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Credits = 10;
        await MicrosoftUserRepository.CreateAsync(user);

        var observedUsers = new ChangeFeedRecorder();
        await using var processor = MicrosoftUserRepository.CreateChangeFeedProcessor(
            NewProcessorName(),
            observedUsers.Handle,
            NewProcessorOptions());
        await processor.StartAsync();

        // Act: a patch writes one field, so a feed carrying only what was written would carry only that
        await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Increment(x => x.Credits, 5));

        // Assert: every field is there, which is what makes a projection immune to partial updates
        await WaitUntilAsync(() => observedUsers.For(partitionKey).Any(x => x.Id == user.Id));
        var observedUser = observedUsers.For(partitionKey).Last(x => x.Id == user.Id);
        observedUser.Credits.ShouldBe(15);
        observedUser.Firstname.ShouldBe(user.Firstname);
        observedUser.Lastname.ShouldBe(user.Lastname);
        observedUser.TenantId.ShouldBe(partitionKey);
    }

    /// <remarks>
    ///     There is deliberately no counterpart asserting that a processor <em>without</em>
    ///     <see cref="ChangeFeedProcessorOptions.StartFromBeginning"/> skips the documents written
    ///     before it started. The vNext Cosmos DB emulator replays the container regardless of the
    ///     start position - verified against the Cosmos SDK directly, with no library code involved -
    ///     so the assertion would fail here for a reason that has nothing to do with this library.
    ///     The in-memory provider covers it in <c>InMemoryChangeFeedTests</c>.
    /// </remarks>
    [Fact]
    public async Task ChangeFeedProcessor_ShouldReplayTheStoredDocumentsWhenStartedFromBeginning()
    {
        // Arrange
        await PrepareChangeFeedAsync();
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        var options = NewProcessorOptions();
        options.StartFromBeginning = true;
        var observedUsers = new ChangeFeedRecorder();

        // Act: the document existed before the processor did
        await using var processor = MicrosoftUserRepository.CreateChangeFeedProcessor(
            NewProcessorName(),
            observedUsers.Handle,
            options);
        await processor.StartAsync();

        // Assert
        await WaitUntilAsync(() => observedUsers.For(partitionKey).Any(x => x.Id == user.Id));
    }

    [Fact]
    public async Task ChangeFeedProcessor_ShouldStopObservingWritesOnceItWasStopped()
    {
        // Arrange
        await PrepareChangeFeedAsync();
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var observedUsers = new ChangeFeedRecorder();
        var processor = MicrosoftUserRepository.CreateChangeFeedProcessor(
            NewProcessorName(),
            observedUsers.Handle,
            NewProcessorOptions());
        await processor.StartAsync();

        var userWrittenWhileRunning = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));
        await WaitUntilAsync(() => observedUsers.For(partitionKey).Any(x => x.Id == userWrittenWhileRunning.Id));

        // Act
        await processor.StopAsync();
        var userWrittenAfterStopping = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Assert: a stopped processor reads nothing, and stopping it twice is not an error
        await Task.Delay(TimeSpan.FromSeconds(2));
        observedUsers.For(partitionKey).ShouldNotContain(x => x.Id == userWrittenAfterStopping.Id);
        await processor.StopAsync();
    }

    [Fact]
    public void CreateChangeFeedProcessor_ShouldThrowWhenTheProcessorNameIsEmpty()
    {
        // Arrange
        var observedUsers = new ChangeFeedRecorder();

        // Act & Assert
        Should.Throw<UnexpectedErrorException>(
                () =>
                {
                    MicrosoftUserRepository.CreateChangeFeedProcessor(
                        " ",
                        observedUsers.Handle);
                })
            .Code.ShouldBe("ChangeFeedProcessorNameIsEmpty");
    }

    [Fact]
    public void CreateAllVersionsAndDeletesChangeFeedProcessor_ShouldThrowWhenStartedFromBeginning()
    {
        // Arrange
        var observedChanges = new AllVersionsAndDeletesChangeFeedRecorder();
        var options = new ChangeFeedProcessorOptions { StartFromBeginning = true };

        // Act & Assert: the previous versions only exist inside the retention window, so there is no
        // beginning of the container to read from
        Should.Throw<UnexpectedErrorException>(
                () =>
                {
                    MicrosoftUserRepository.CreateAllVersionsAndDeletesChangeFeedProcessor(
                        NewProcessorName(),
                        observedChanges.Handle,
                        options);
                })
            .Code.ShouldBe("ChangeFeedStartFromBeginningNotSupported");
    }

    /// <summary>
    ///     A name per test, so a test never inherits the checkpoint - or the leases - of a previous
    ///     run and silently reads from where that one stopped.
    /// </summary>
    private static string NewProcessorName()
    {
        return $"test-{Guid.NewGuid():N}";
    }

    private static ChangeFeedProcessorOptions NewProcessorOptions()
    {
        return new ChangeFeedProcessorOptions
        {
            // shorter than the default of the Cosmos SDK, so the tests spend their time asserting
            // rather than waiting for the next read
            PollInterval = TimeSpan.FromMilliseconds(500)
        };
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var stopwatch = Stopwatch.StartNew();

        while (!condition())
        {
            if (stopwatch.Elapsed > ChangeFeedTimeout)
            {
                throw new TimeoutException(
                    $"The change feed did not deliver the expected changes within {ChangeFeedTimeout}");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50));
        }
    }

    /// <summary>
    ///     Collects what a change feed handed over. The handler runs on a thread of the processor,
    ///     so everything it writes is guarded and everything a test reads is a copy.
    /// </summary>
    private class ChangeFeedRecorder
    {
        private readonly object _gate = new object();
        private readonly List<User> _changes = new List<User>();
        private readonly List<ChangeFeedContext> _contexts = new List<ChangeFeedContext>();

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

        public Task Handle(
            IReadOnlyCollection<User> changes,
            ChangeFeedContext context,
            CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _changes.AddRange(changes);
                _contexts.Add(context);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        ///     The changes of one partition, in the order they arrived. Every provider reads the whole
        ///     collection, which the other tests write to as well, so a test only ever looks at the
        ///     partition it owns.
        /// </summary>
        public List<User> For(string partitionKey)
        {
            lock (_gate)
            {
                return _changes
                    .Where(change => change.TenantId == partitionKey)
                    .ToList();
            }
        }
    }

    private class AllVersionsAndDeletesChangeFeedRecorder
    {
        public Task Handle(
            IReadOnlyCollection<DatabaseChange<User>> changes,
            ChangeFeedContext context,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
