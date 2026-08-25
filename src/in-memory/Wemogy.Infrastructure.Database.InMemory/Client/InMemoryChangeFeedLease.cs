namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     What the in-memory store remembers about a processor name between two runs, standing in
    ///     for the lease documents Cosmos DB keeps in its lease container.
    /// </summary>
    internal class InMemoryChangeFeedLease
    {
        public InMemoryChangeFeedLease(long checkpoint, bool replayPending)
        {
            Checkpoint = checkpoint;
            ReplayPending = replayPending;
        }

        /// <summary>
        ///     The position everything up to which was handed over and handled.
        /// </summary>
        public long Checkpoint { get; set; }

        /// <summary>
        ///     Whether the documents the store already held when this processor first started still
        ///     have to be replayed.
        ///     <para>
        ///         Kept apart from <see cref="Checkpoint"/> because the two answer different
        ///         questions: the checkpoint says where in the log to carry on, the replay is the
        ///         snapshot a processor started with
        ///         <see cref="Core.Models.ChangeFeedProcessorOptions.StartFromBeginning"/> owes its
        ///         handler. Without it, a replay interrupted by a failure or a shutdown would be
        ///         skipped on the next start, because the checkpoint alone looks like a processor
        ///         that already caught up.
        ///     </para>
        /// </summary>
        public bool ReplayPending { get; set; }
    }
}
