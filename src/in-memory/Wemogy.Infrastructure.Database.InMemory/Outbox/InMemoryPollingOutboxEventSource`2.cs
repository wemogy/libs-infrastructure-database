using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;
using Wemogy.Infrastructure.Database.Outbox.Setup;

namespace Wemogy.Infrastructure.Database.InMemory.Outbox;

internal class InMemoryPollingOutboxEventSource<TOutboxEvent, TPayload> : IOutboxEventSource<TOutboxEvent, TPayload>
    where TOutboxEvent : OutboxEventBase<TPayload>, IEntityBase
    where TPayload : class
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxProcessorOptions _options;

    public InMemoryPollingOutboxEventSource(
        IServiceScopeFactory scopeFactory,
        OutboxProcessorOptions options)
    {
        _scopeFactory = scopeFactory;
        _options = options;
    }

    public async Task StartAsync(Func<TOutboxEvent, Task> onEvent, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(onEvent, cancellationToken);
                await Task.Delay(_options.PollingInterval, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task PollAsync(Func<TOutboxEvent, Task> onEvent, CancellationToken cancellationToken)
    {
        var pending = new List<TOutboxEvent>();

        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDatabaseRepository<TOutboxEvent>>();

        await repository.IterateAsync(
            e => e.Status == OutboxStatus.Pending,
            pending.Add,
            cancellationToken);

        foreach (var evt in pending)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            await onEvent(evt);
        }
    }
}
