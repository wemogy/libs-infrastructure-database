using Microsoft.Extensions.Hosting;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;

namespace Wemogy.Infrastructure.Database.Outbox.Services;

internal class OutboxProcessorService<TOutboxEvent, TPayload> : BackgroundService
    where TOutboxEvent : OutboxEventBase<TPayload>, IEntityBase
    where TPayload : class
{
    protected override System.Threading.Tasks.Task ExecuteAsync(System.Threading.CancellationToken stoppingToken)
        => System.Threading.Tasks.Task.CompletedTask;
}
