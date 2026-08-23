namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     A single operation of an <see cref="InMemoryTransactionalBatch{TEntity}"/>. The batch only
    ///     records its operations; they are validated and applied when the batch is executed.
    /// </summary>
    /// <typeparam name="TEntity">The entity type the operation acts on</typeparam>
    internal class InMemoryTransactionalBatchOperation<TEntity>
        where TEntity : class
    {
        private InMemoryTransactionalBatchOperation(
            InMemoryTransactionalBatchOperationKind kind,
            TEntity? entity,
            string? id)
        {
            Kind = kind;
            Entity = entity;
            Id = id;
        }

        public InMemoryTransactionalBatchOperationKind Kind { get; }

        /// <summary>
        ///     The entity to write, null for a <see cref="InMemoryTransactionalBatchOperationKind.Delete"/>.
        /// </summary>
        public TEntity? Entity { get; }

        /// <summary>
        ///     The id to delete, null for every other kind, which carries the id on its entity.
        /// </summary>
        public string? Id { get; }

        public static InMemoryTransactionalBatchOperation<TEntity> Create(TEntity entity)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Create,
                entity,
                null);
        }

        public static InMemoryTransactionalBatchOperation<TEntity> Replace(TEntity entity)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Replace,
                entity,
                null);
        }

        public static InMemoryTransactionalBatchOperation<TEntity> Upsert(TEntity entity)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Upsert,
                entity,
                null);
        }

        public static InMemoryTransactionalBatchOperation<TEntity> Delete(string id)
        {
            return new InMemoryTransactionalBatchOperation<TEntity>(
                InMemoryTransactionalBatchOperationKind.Delete,
                null,
                id);
        }
    }
}
