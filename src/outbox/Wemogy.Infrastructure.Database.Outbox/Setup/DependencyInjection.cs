using System;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.Outbox.Abstractions;
using Wemogy.Infrastructure.Database.Outbox.Entities;
using Wemogy.Infrastructure.Database.Outbox.Services;

namespace Wemogy.Infrastructure.Database.Outbox.Setup;

public static class DependencyInjection
{
    public static DatabaseSetupEnvironment AddOutboxProcessor<TOutboxEvent, TPayload, THandler>(
        this DatabaseSetupEnvironment environment,
        string containerName,
        Action<OutboxProcessorOptions>? configure = null)
        where TOutboxEvent : OutboxEventBase<TPayload>, IEntityBase
        where THandler : class, IOutboxEventHandler<TPayload>
        where TPayload : class
    {
        var options = new OutboxProcessorOptions();
        configure?.Invoke(options);

        var repoDelegate = environment.CreateOutboxRepositoryDelegate<TOutboxEvent>(containerName);
        environment.Services.AddScoped(_ => repoDelegate(_));

        environment.Services.AddScoped<IOutboxEventHandler<TPayload>, THandler>();
        environment.Services.AddSingleton(options);
        environment.Services.AddHostedService<OutboxProcessorService<TOutboxEvent, TPayload>>();

        return environment;
    }
}
