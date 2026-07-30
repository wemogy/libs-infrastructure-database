using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Outbox.Abstractions;

namespace Wemogy.Infrastructure.Database.Outbox.UnitTests.Fakes;

public class TestOrderHandler : IOutboxEventHandler<TestOrderPayload>
{
    public List<TestOrderPayload> HandledPayloads { get; } = new List<TestOrderPayload>();
    public bool ShouldThrow { get; set; }

    public Task HandleAsync(TestOrderPayload payload, CancellationToken cancellationToken = default)
    {
        if (ShouldThrow)
        {
            throw new System.Exception("Handler failure");
        }

        HandledPayloads.Add(payload);
        return Task.CompletedTask;
    }
}
