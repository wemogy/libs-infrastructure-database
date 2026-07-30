using System;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Cosmos.Batch
{
    internal sealed class CosmosActionBatchOperation : IBatchOperation
    {
        private readonly Action<TransactionalBatch> _action;

        public CosmosActionBatchOperation(Action<TransactionalBatch> action)
        {
            _action = action;
        }

        public void ApplyTo(TransactionalBatch batch) => _action(batch);
    }
}
