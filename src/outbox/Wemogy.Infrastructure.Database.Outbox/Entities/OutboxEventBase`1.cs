using System;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Outbox.Entities;

public abstract class OutboxEventBase<TPayload> : EntityBase
{
    [ETag]
    public string? ETag { get; set; }

    public TPayload Payload { get; set; } = default!;

    public OutboxStatus Status { get; set; } = OutboxStatus.Pending;

    public int RetryCount { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? LastAttemptAt { get; set; }
}
