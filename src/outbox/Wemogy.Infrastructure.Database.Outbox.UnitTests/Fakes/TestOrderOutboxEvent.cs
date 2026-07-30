using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Outbox.Entities;

namespace Wemogy.Infrastructure.Database.Outbox.UnitTests.Fakes;

public class TestOrderOutboxEvent : OutboxEventBase<TestOrderPayload>
{
    [PartitionKey]
    public string TenantId { get; set; } = "default";
}
