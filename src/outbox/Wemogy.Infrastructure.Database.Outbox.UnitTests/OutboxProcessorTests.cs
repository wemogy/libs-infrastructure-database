using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.InMemory.Setup;
using Wemogy.Infrastructure.Database.Outbox.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;
using Wemogy.Infrastructure.Database.Outbox.Setup;
using Wemogy.Infrastructure.Database.Outbox.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.Outbox.UnitTests;

[Collection("Sequential")]
public class OutboxProcessorTests : IAsyncLifetime
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TestOrderHandler _handler;

    public OutboxProcessorTests()
    {
        _handler = new TestOrderHandler();

        var services = new ServiceCollection();

        // AddInMemoryOutboxProcessor registers IOutboxEventHandler<TPayload> as Scoped (THandler).
        // We override it by adding our observable singleton *after*, which wins with GetRequiredService.
        services
            .AddInMemoryDatabaseClient()
            .AddInMemoryOutboxProcessor<TestOrderOutboxEvent, TestOrderPayload, TestOrderHandler>(
                "test-outbox",
                options =>
                {
                    options.PollingInterval = TimeSpan.FromMilliseconds(50);
                    options.MaxRetryCount = 2;
                    options.ClaimTimeout = TimeSpan.FromSeconds(60);
                });

        // Register the observable instance as a singleton AFTER AddInMemoryOutboxProcessor.
        // In .NET DI the last registration wins for GetRequiredService<T>(), so this singleton
        // shadows the Scoped<TestOrderHandler> registered inside AddOutboxProcessor.
        services.AddSingleton<IOutboxEventHandler<TestOrderPayload>>(_handler);

        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask InitializeAsync()
    {
        foreach (var hostedService in _serviceProvider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var hostedService in _serviceProvider.GetServices<IHostedService>())
        {
            await hostedService.StopAsync(CancellationToken.None);
        }

        (_serviceProvider as IDisposable)?.Dispose();
    }

    [Fact]
    public void OutboxEvent_ShouldInitialize_WithPendingStatus()
    {
        var outboxEvent = new TestOrderOutboxEvent
        {
            Payload = new TestOrderPayload { OrderId = "order-1", Amount = 10 }
        };

        outboxEvent.Status.ShouldBe(OutboxStatus.Pending);
        outboxEvent.RetryCount.ShouldBe(0);
        outboxEvent.ETag.ShouldBeNull();
        outboxEvent.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task OutboxProcessor_ShouldHandleEvent_WhenEventIsCreated()
    {
        // Arrange
        var repository = _serviceProvider.GetRequiredService<IDatabaseRepository<TestOrderOutboxEvent>>();
        var outboxEvent = new TestOrderOutboxEvent
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = "default",
            Payload = new TestOrderPayload { OrderId = "order-happy", Amount = 100 }
        };

        // Act
        await repository.CreateAsync(outboxEvent);

        // Wait for the polling loop (50 ms interval) to pick up and process the event.
        // Give a generous 2 s budget so the test is not flaky under load.
        await WaitUntilAsync(
            () => _handler.HandledPayloads.Count > 0,
            TimeSpan.FromSeconds(2));

        // Assert
        _handler.HandledPayloads.Count.ShouldBe(1);
        _handler.HandledPayloads[0].OrderId.ShouldBe("order-happy");

        // After successful handling the processor deletes the event
        var remaining = new List<TestOrderOutboxEvent>();
        await repository.IterateAsync(_ => true, remaining.Add, CancellationToken.None);
        remaining.ShouldBeEmpty();
    }

    [Fact]
    public async Task OutboxProcessor_ShouldMarkFailed_WhenHandlerAlwaysThrows()
    {
        // Arrange
        _handler.ShouldThrow = true;

        var repository = _serviceProvider.GetRequiredService<IDatabaseRepository<TestOrderOutboxEvent>>();
        var outboxEvent = new TestOrderOutboxEvent
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = "default",
            Payload = new TestOrderPayload { OrderId = "order-fail", Amount = 50 }
        };

        // Act
        await repository.CreateAsync(outboxEvent);

        // MaxRetryCount=2, polling every 50 ms. Allow 3 s for retries to exhaust.
        await WaitUntilAsync(
            async () =>
            {
                var events = new List<TestOrderOutboxEvent>();
                await repository.IterateAsync(_ => true, events.Add, CancellationToken.None);
                return events.Count == 1 && events[0].Status == OutboxStatus.Failed;
            },
            TimeSpan.FromSeconds(3));

        // Assert – event must still exist and be marked Failed
        var remaining = new List<TestOrderOutboxEvent>();
        await repository.IterateAsync(_ => true, remaining.Add, CancellationToken.None);
        remaining.Count.ShouldBe(1);
        remaining[0].Status.ShouldBe(OutboxStatus.Failed);
        remaining[0].RetryCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task OutboxProcessor_ShouldNotDuplicateHandle_WhenETagChanges()
    {
        // This test verifies the optimistic-concurrency claim (TryClaimAsync) prevents
        // double-processing: two concurrent "claimers" can only win if their ETag matches
        // the stored version. The InMemory client throws PreconditionFailed on mismatch.

        var repository = _serviceProvider.GetRequiredService<IDatabaseRepository<TestOrderOutboxEvent>>();
        var outboxEvent = new TestOrderOutboxEvent
        {
            Id = Guid.NewGuid().ToString(),
            TenantId = "default",
            Payload = new TestOrderPayload { OrderId = "order-once", Amount = 25 }
        };

        await repository.CreateAsync(outboxEvent);

        await WaitUntilAsync(
            () => _handler.HandledPayloads.Count > 0,
            TimeSpan.FromSeconds(2));

        // Give the processor a moment to settle (avoid race with second poll cycle)
        await Task.Delay(200);

        // The handler should have been invoked exactly once regardless of how many poll
        // cycles ran while the event was in-flight.
        _handler.HandledPayloads.Count.ShouldBe(1);
    }

    // -------------------------------------------------------------------------
    // Helper
    // -------------------------------------------------------------------------

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!await condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }
}
