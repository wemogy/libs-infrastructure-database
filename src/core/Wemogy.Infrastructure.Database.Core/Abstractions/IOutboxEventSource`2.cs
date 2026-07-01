using System;
using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IOutboxEventSource<TOutboxEvent, TPayload>
    where TOutboxEvent : class
{
    Task StartAsync(
        Func<TOutboxEvent, Task> onEvent,
        CancellationToken cancellationToken);
}
