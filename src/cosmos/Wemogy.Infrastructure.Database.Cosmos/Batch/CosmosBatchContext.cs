using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Cosmos.Batch
{
    internal sealed class CosmosBatchContext : IBatchContext
    {
        private readonly Container _container;
        private readonly PartitionKey _partitionKey;
        private readonly List<CosmosActionBatchOperation> _operations = new List<CosmosActionBatchOperation>();

        public CosmosBatchContext(Container container, PartitionKey partitionKey)
        {
            _container = container;
            _partitionKey = partitionKey;
        }

        public void Add(IBatchOperation operation)
        {
            _operations.Add((CosmosActionBatchOperation)operation);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var batch = _container.CreateTransactionalBatch(_partitionKey);
            foreach (var op in _operations)
            {
                op.ApplyTo(batch);
            }

            using var response = await batch.ExecuteAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw Error.Unexpected(
                    "BatchExecutionFailed",
                    $"Cosmos transactional batch failed with status {(int)response.StatusCode}: {response.ErrorMessage}");
            }
        }
    }
}
