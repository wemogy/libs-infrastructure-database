using System.Collections.Generic;
using System.Threading;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     The lock guarding the store of one entity type. Each closed generic
    ///     <see cref="InMemoryDatabaseClient{TEntity}"/> keeps its own store and its own gate, so a
    ///     write to one entity type does not block a write to another.
    ///     <para>
    ///         A mixed-type partition batch writes to several stores at once and has to appear atomic
    ///         across all of them, so it holds the gates of every type it touches for the duration -
    ///         but only those. The rank exists to make that safe: gates are always entered in
    ///         ascending rank order, which is a total order over all gates, so two batches whose type
    ///         sets overlap cannot each hold what the other waits for.
    ///     </para>
    /// </summary>
    internal sealed class InMemoryStoreGate
    {
        private static int _lastRank;

        public InMemoryStoreGate()
        {
            Rank = Interlocked.Increment(ref _lastRank);
        }

        /// <summary>
        ///     Where this gate sits in the order gates have to be entered in. Assigned in the order
        ///     the entity types are first used, which is arbitrary but stable for the lifetime of the
        ///     process - and being a total order is all that is asked of it.
        /// </summary>
        public int Rank { get; }

        /// <summary>
        ///     Enters every gate, in ascending rank order. The caller has to pass the same list and
        ///     the same flags to <see cref="ExitAll"/> from a finally block.
        ///     <para>
        ///         The list is sorted in place, so the order the caller collected the gates in - which
        ///         follows the order the entity types happened to be added to a batch - cannot affect
        ///         the order they are entered in. That is what keeps concurrent batches deadlock-free.
        ///     </para>
        /// </summary>
        /// <param name="gates">The gates to enter, sorted in place</param>
        /// <param name="entered">Flags recording which gates were entered, one per gate</param>
        public static void EnterAll(List<InMemoryStoreGate> gates, bool[] entered)
        {
            gates.Sort((left, right) => left.Rank.CompareTo(right.Rank));

            for (var index = 0; index < gates.Count; index++)
            {
                Monitor.Enter(
                    gates[index],
                    ref entered[index]);
            }
        }

        /// <summary>
        ///     Exits the gates <see cref="EnterAll"/> entered, in reverse order. Safe to call when
        ///     entering only got part of the way through.
        /// </summary>
        /// <param name="gates">The gates as <see cref="EnterAll"/> left them</param>
        /// <param name="entered">The flags <see cref="EnterAll"/> filled in</param>
        public static void ExitAll(List<InMemoryStoreGate> gates, bool[] entered)
        {
            for (var index = gates.Count - 1; index >= 0; index--)
            {
                if (entered[index])
                {
                    Monitor.Exit(gates[index]);
                }
            }
        }
    }
}
