using System.Collections.Generic;
using System.Threading;
using Shouldly;
using Wemogy.Infrastructure.Database.InMemory.Client;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Client;

/// <summary>
///     The rule that keeps a mixed-type partition batch from deadlocking: the gates of the stores it
///     writes are entered in one order, whatever order the caller collected them in. Tested here
///     rather than by racing batches, because a deadlock between two batches is a window of a few
///     microseconds wide - a test that races them passes almost every time even when the rule is
///     broken, which is worse than no test at all.
/// </summary>
public class InMemoryStoreGateTests
{
    [Fact]
    public void EnterAll_ShouldEnterInRankOrderWhateverOrderItWasGiven()
    {
        // Arrange: ranks are handed out in construction order
        var lower = new InMemoryStoreGate();
        var higher = new InMemoryStoreGate();
        lower.Rank.ShouldBeLessThan(higher.Rank);

        // the order a batch would collect them in if it saw the higher-ranked type first
        var gates = new List<InMemoryStoreGate> { higher, lower };
        var entered = new bool[gates.Count];

        // Act
        try
        {
            InMemoryStoreGate.EnterAll(
                gates,
                entered);

            // Assert: reordered by rank, so a batch that sees its types in the opposite order still
            // enters the gates in this one
            gates.ShouldBe(new[] { lower, higher });
            entered.ShouldAllBe(flag => flag);
        }
        finally
        {
            InMemoryStoreGate.ExitAll(
                gates,
                entered);
        }
    }

    [Fact]
    public void ExitAll_ShouldReleaseEveryGateItEntered()
    {
        // Arrange
        var first = new InMemoryStoreGate();
        var second = new InMemoryStoreGate();
        var gates = new List<InMemoryStoreGate> { first, second };
        var entered = new bool[gates.Count];

        // Act
        InMemoryStoreGate.EnterAll(
            gates,
            entered);
        InMemoryStoreGate.ExitAll(
            gates,
            entered);

        // Assert: nothing was left held, so the next batch is not blocked by the last one. Checked
        // from another thread, because a Monitor is reentrant and this one would succeed either way
        var acquired = new bool[gates.Count];
        var probe = new Thread(() =>
        {
            for (var index = 0; index < gates.Count; index++)
            {
                acquired[index] = Monitor.TryEnter(gates[index]);
                if (acquired[index])
                {
                    Monitor.Exit(gates[index]);
                }
            }
        });

        probe.Start();
        probe.Join();

        acquired.ShouldAllBe(flag => flag);
    }

    [Fact]
    public void ExitAll_ShouldTolerateGatesThatWereNeverEntered()
    {
        // Arrange: the flags EnterAll would have left behind had it thrown part way through
        var gates = new List<InMemoryStoreGate> { new InMemoryStoreGate(), new InMemoryStoreGate() };
        var entered = new bool[gates.Count];

        Monitor.Enter(
            gates[0],
            ref entered[0]);

        // Act & Assert: releasing what was taken and leaving the rest alone, rather than throwing a
        // SynchronizationLockException that would bury the original failure
        Should.NotThrow(() => InMemoryStoreGate.ExitAll(
            gates,
            entered));
    }
}
