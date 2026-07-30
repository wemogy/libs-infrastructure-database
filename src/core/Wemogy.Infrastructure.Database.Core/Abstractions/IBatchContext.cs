using System.Threading;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     Accumulates <see cref="IBatchOperation"/> instances from one or more repositories
///     and executes them as a single unit.
///     For the Cosmos provider this maps to <c>TransactionalBatch</c> (atomic, same container,
///     same partition key). For InMemory the operations are executed sequentially with no atomicity
///     guarantee. MongoDB throws <see cref="System.NotSupportedException"/>.
/// </summary>
public interface IBatchContext
{
    /// <summary>
    ///     Adds an operation to the batch. All operations in one batch must originate from
    ///     repositories backed by the same provider and the same underlying container.
    /// </summary>
    void Add(IBatchOperation operation);

    /// <summary>Executes all accumulated operations.</summary>
    Task ExecuteAsync(CancellationToken cancellationToken = default);
}
