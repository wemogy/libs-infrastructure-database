using System;

namespace Wemogy.Infrastructure.Database.Outbox.Setup;

public class OutboxProcessorOptions
{
    public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(10);

    public int MaxRetryCount { get; set; } = 3;

    public TimeSpan ClaimTimeout { get; set; } = TimeSpan.FromSeconds(60);
}
