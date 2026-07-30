using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Outbox.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;
using Wemogy.Infrastructure.Database.Outbox.Setup;

namespace Wemogy.Infrastructure.Database.Outbox.Services;

internal class OutboxProcessorService<TOutboxEvent, TPayload> : BackgroundService
    where TOutboxEvent : OutboxEventBase<TPayload>, IEntityBase
    where TPayload : class
{
    private readonly IOutboxEventSource<TOutboxEvent, TPayload> _source;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboxProcessorOptions _options;
    private readonly PropertyInfo? _partitionKeyProperty;

    public OutboxProcessorService(
        IOutboxEventSource<TOutboxEvent, TPayload> source,
        IServiceScopeFactory scopeFactory,
        OutboxProcessorOptions options)
    {
        _source = source;
        _scopeFactory = scopeFactory;
        _options = options;
        _partitionKeyProperty = typeof(TOutboxEvent).GetPropertyByCustomAttribute<PartitionKeyAttribute>();
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sourceTask = _source.StartAsync(
            outboxEvent => ProcessEventAsync(outboxEvent, stoppingToken),
            stoppingToken);

        var recoveryTask = RunRecoveryScanLoopAsync(stoppingToken);

        return Task.WhenAll(sourceTask, recoveryTask);
    }

    private async Task ProcessEventAsync(TOutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDatabaseRepository<TOutboxEvent>>();

        var claimed = await TryClaimAsync(outboxEvent, repository);
        if (!claimed)
        {
            return;
        }

        var handler = scope.ServiceProvider.GetRequiredService<IOutboxEventHandler<TPayload>>();
        try
        {
            await handler.HandleAsync(outboxEvent.Payload, cancellationToken);
            await repository.DeleteAsync(outboxEvent.Id, GetPartitionKeyValue(outboxEvent));
        }
        catch (Exception ex)
        {
            outboxEvent.RetryCount++;
            outboxEvent.ErrorMessage = ex.Message;
            outboxEvent.LastAttemptAt = DateTime.UtcNow;
            outboxEvent.Status = outboxEvent.RetryCount >= _options.MaxRetryCount
                ? OutboxStatus.Failed
                : OutboxStatus.Pending;
            await repository.ReplaceAsync(outboxEvent);
        }
    }

    private async Task<bool> TryClaimAsync(TOutboxEvent outboxEvent, IDatabaseRepository<TOutboxEvent> repository)
    {
        try
        {
            outboxEvent.Status = OutboxStatus.Processing;
            outboxEvent.LastAttemptAt = DateTime.UtcNow;
            await repository.ReplaceAsync(outboxEvent);
            return true;
        }
        catch (PreconditionFailedErrorException)
        {
            return false;
        }
    }

    private async Task RunRecoveryScanLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_options.PollingInterval, cancellationToken);
                await RunRecoveryScanAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunRecoveryScanAsync(CancellationToken cancellationToken)
    {
        var claimDeadline = DateTime.UtcNow - _options.ClaimTimeout;
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IDatabaseRepository<TOutboxEvent>>();

        var stuckEvents = new List<TOutboxEvent>();
        await repository.IterateAsync(
            e => e.Status == OutboxStatus.Processing && e.LastAttemptAt < claimDeadline,
            stuckEvents.Add,
            cancellationToken);

        foreach (var stuckEvent in stuckEvents)
        {
            stuckEvent.Status = OutboxStatus.Pending;
            stuckEvent.ETag = null;
            await repository.ReplaceAsync(stuckEvent);
        }
    }

    private string GetPartitionKeyValue(TOutboxEvent entity)
    {
        if (_partitionKeyProperty == null)
        {
            throw new InvalidOperationException(
                $"Entity {typeof(TOutboxEvent).Name} does not have a [PartitionKey] property.");
        }

        return (string)_partitionKeyProperty.GetValue(entity)!;
    }
}
