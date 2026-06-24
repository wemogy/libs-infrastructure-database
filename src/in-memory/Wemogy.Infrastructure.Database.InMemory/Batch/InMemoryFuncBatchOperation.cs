using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.InMemory.Batch
{
    internal sealed class InMemoryFuncBatchOperation : IBatchOperation
    {
        private readonly Func<Task> _operation;

        public InMemoryFuncBatchOperation(Func<Task> operation)
        {
            _operation = operation;
        }

        public Task ExecuteAsync() => _operation();
    }
}
