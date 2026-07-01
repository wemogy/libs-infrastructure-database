using System;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Wemogy.Infrastructure.Database.InMemory.Outbox;
using Wemogy.Infrastructure.Database.Outbox.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;
using Wemogy.Infrastructure.Database.Outbox.Setup;

namespace Wemogy.Infrastructure.Database.InMemory.Setup
{
    public static class DependencyInjection
    {
        public static DatabaseSetupEnvironment AddInMemoryDatabaseClient(this IServiceCollection serviceCollection)
        {
            return new DatabaseSetupEnvironment(
                serviceCollection,
                new InMemoryDatabaseClientFactory());
        }

        public static DatabaseSetupEnvironment AddInMemoryOutboxProcessor<TOutboxEvent, TPayload, THandler>(
            this DatabaseSetupEnvironment environment,
            string containerName,
            Action<OutboxProcessorOptions>? configure = null)
            where TOutboxEvent : OutboxEventBase<TPayload>, IEntityBase
            where THandler : class, IOutboxEventHandler<TPayload>
            where TPayload : class
        {
            environment.Services.AddSingleton<IOutboxEventSource<TOutboxEvent, TPayload>>(sp =>
                new InMemoryPollingOutboxEventSource<TOutboxEvent, TPayload>(
                    sp.GetRequiredService<IServiceScopeFactory>(),
                    sp.GetRequiredService<OutboxProcessorOptions>()));

            return environment.AddOutboxProcessor<TOutboxEvent, TPayload, THandler>(containerName, configure);
        }
    }
}
