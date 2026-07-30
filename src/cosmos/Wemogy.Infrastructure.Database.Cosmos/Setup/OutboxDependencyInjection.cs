using System;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Cosmos.Outbox;
using Wemogy.Infrastructure.Database.Outbox.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;
using Wemogy.Infrastructure.Database.Outbox.Setup;

namespace Wemogy.Infrastructure.Database.Cosmos.Setup
{
    public static class OutboxDependencyInjection
    {
        public static DatabaseSetupEnvironment AddOutboxProcessor<TOutboxEvent, TPayload, THandler>(
            this DatabaseSetupEnvironment environment,
            CosmosClient cosmosClient,
            string databaseName,
            string monitoredContainerName,
            string leaseContainerName,
            Action<OutboxProcessorOptions>? configure = null)
            where TOutboxEvent : OutboxEventBase<TPayload>, IEntityBase
            where THandler : class, IOutboxEventHandler<TPayload>
            where TPayload : class
        {
            environment.Services.AddSingleton<IOutboxEventSource<TOutboxEvent, TPayload>>(sp =>
            {
                var monitoredContainer = cosmosClient.GetDatabase(databaseName).GetContainer(monitoredContainerName);
                var leaseContainer = cosmosClient.GetDatabase(databaseName).GetContainer(leaseContainerName);
                return new CosmosChangeFeedOutboxEventSource<TOutboxEvent, TPayload>(
                    monitoredContainer,
                    leaseContainer);
            });

            return environment.AddOutboxProcessor<TOutboxEvent, TPayload, THandler>(
                monitoredContainerName,
                configure);
        }
    }
}
