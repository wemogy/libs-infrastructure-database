using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;

namespace Wemogy.Infrastructure.Database.Cosmos.Outbox
{
    internal class CosmosChangeFeedOutboxEventSource<TOutboxEvent, TPayload> : IOutboxEventSource<TOutboxEvent, TPayload>
        where TOutboxEvent : OutboxEventBase<TPayload>, IEntityBase
        where TPayload : class
    {
        private readonly Container _monitoredContainer;
        private readonly Container _leaseContainer;
        private readonly string _processorName;

        public CosmosChangeFeedOutboxEventSource(
            Container monitoredContainer,
            Container leaseContainer,
            string processorName = "outbox-processor")
        {
            _monitoredContainer = monitoredContainer;
            _leaseContainer = leaseContainer;
            _processorName = processorName;
        }

        public async Task StartAsync(Func<TOutboxEvent, Task> onEvent, CancellationToken cancellationToken)
        {
            var processor = _monitoredContainer
                .GetChangeFeedProcessorBuilder<TOutboxEvent>(
                    _processorName,
                    async (_, changes, _) =>
                    {
                        foreach (var change in changes.Where(c => c.Status == OutboxStatus.Pending))
                        {
                            await onEvent(change);
                        }
                    })
                .WithInstanceName(Environment.MachineName)
                .WithLeaseContainer(_leaseContainer)
                .Build();

            await processor.StartAsync();

            var tcs = new TaskCompletionSource();
            cancellationToken.Register(() => tcs.TrySetResult());
            await tcs.Task;

            await processor.StopAsync();
        }
    }
}
