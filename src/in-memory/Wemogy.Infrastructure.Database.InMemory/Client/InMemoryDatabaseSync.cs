namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     The one lock every in-memory store is guarded by. Each closed generic
    ///     <see cref="InMemoryDatabaseClient{TEntity}"/> keeps its own static store, so a per-type
    ///     lock would be enough for a single-type operation. A mixed-type partition batch, however,
    ///     writes to several of those stores at once and has to appear atomic across all of them, so
    ///     every store shares this one process-wide lock instead. The cost is that writes to
    ///     unrelated entity types no longer run in parallel, which the in-memory provider - a test
    ///     and development stand-in for a real database - can afford.
    /// </summary>
    internal static class InMemoryDatabaseSync
    {
        public static readonly object Gate = new object();
    }
}
