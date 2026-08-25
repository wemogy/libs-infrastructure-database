using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     One write recorded on the change log of the in-memory store, in the order it was applied.
    /// </summary>
    /// <typeparam name="TEntity">The entity type the store holds</typeparam>
    internal class InMemoryChangeRecord<TEntity>
        where TEntity : class
    {
        public InMemoryChangeRecord(
            long sequence,
            PartitionKeyValue partitionKey,
            string id,
            DatabaseChangeOperation operation,
            TEntity? current,
            TEntity? previous)
        {
            Sequence = sequence;
            PartitionKey = partitionKey;
            Id = id;
            Operation = operation;
            Current = current;
            Previous = previous;
        }

        /// <summary>
        ///     Position of this write in the store, counted across all partitions. Stands in for the
        ///     log sequence number Cosmos DB assigns, and is what a processor checkpoints.
        /// </summary>
        public long Sequence { get; }

        public PartitionKeyValue PartitionKey { get; }

        public string Id { get; }

        public DatabaseChangeOperation Operation { get; }

        /// <summary>
        ///     The document after the write, or <c>null</c> for a delete.
        /// </summary>
        public TEntity? Current { get; }

        /// <summary>
        ///     The document before the write, or <c>null</c> for a create.
        /// </summary>
        public TEntity? Previous { get; }
    }
}
