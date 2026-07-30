using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Outbox.Abstractions;

public interface IOutboxEventHandler<TPayload>
{
    Task HandleAsync(TPayload payload, CancellationToken cancellationToken = default);
}
