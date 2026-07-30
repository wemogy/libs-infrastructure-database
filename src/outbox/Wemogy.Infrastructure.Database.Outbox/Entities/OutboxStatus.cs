namespace Wemogy.Infrastructure.Database.Outbox.Entities;

public enum OutboxStatus
{
    Pending,
    Processing,
    Failed
}
