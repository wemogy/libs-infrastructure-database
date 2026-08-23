using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Repositories;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     In-memory implementation of a transactional batch. It records its operations and hands
    ///     them to the client, which validates all of them before it applies any of them, so a
    ///     failing batch leaves the store untouched - the same guarantee Cosmos DB gives.
    /// </summary>
    /// <typeparam name="TEntity">The entity type every operation of the batch acts on</typeparam>
    public class InMemoryTransactionalBatch<TEntity> : DatabaseTransactionalBatchBase<TEntity>
        where TEntity : class
    {
        private readonly InMemoryDatabaseClient<TEntity> _client;

        private readonly List<InMemoryTransactionalBatchOperation<TEntity>> _operations =
            new List<InMemoryTransactionalBatchOperation<TEntity>>();

        /// <summary>
        ///     Initializes a new instance of the <see cref="InMemoryTransactionalBatch{TEntity}"/> class.
        /// </summary>
        /// <param name="client">The client that owns the store the batch is applied to</param>
        /// <param name="partitionKey">The logical partition every operation of the batch acts on</param>
        /// <param name="resolvePartitionKeyValue">Reads the partition key value of an entity</param>
        public InMemoryTransactionalBatch(
            InMemoryDatabaseClient<TEntity> client,
            string partitionKey,
            Func<TEntity, string> resolvePartitionKeyValue)
            : base(partitionKey, resolvePartitionKeyValue)
        {
            _client = client;
        }

        /// <inheritdoc />
        protected override void ApplyCreate(TEntity entity)
        {
            _operations.Add(InMemoryTransactionalBatchOperation<TEntity>.Create(entity));
        }

        /// <inheritdoc />
        protected override void ApplyReplace(TEntity entity)
        {
            _operations.Add(InMemoryTransactionalBatchOperation<TEntity>.Replace(entity));
        }

        /// <inheritdoc />
        protected override void ApplyUpsert(TEntity entity)
        {
            _operations.Add(InMemoryTransactionalBatchOperation<TEntity>.Upsert(entity));
        }

        /// <inheritdoc />
        protected override void ApplyDelete(string id)
        {
            _operations.Add(InMemoryTransactionalBatchOperation<TEntity>.Delete(id));
        }

        /// <inheritdoc />
        protected override Task ExecuteCoreAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _client.ExecuteBatch(
                PartitionKey,
                _operations);

            return Task.CompletedTask;
        }
    }
}
