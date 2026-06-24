using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.InMemory.Batch
{
    internal sealed class InMemoryBatchContext : IBatchContext
    {
        private readonly List<InMemoryFuncBatchOperation> _operations = new List<InMemoryFuncBatchOperation>();

        public void Add(IBatchOperation operation)
        {
            _operations.Add((InMemoryFuncBatchOperation)operation);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            foreach (var op in _operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await op.ExecuteAsync();
            }
        }
    }
}
