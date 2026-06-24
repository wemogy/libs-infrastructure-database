namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     Represents a single pending database operation within a batch.
///     Each provider supplies a concrete implementation that captures the
///     provider-specific closure (e.g. an <c>Action&lt;TransactionalBatch&gt;</c> for Cosmos,
///     a <c>Func&lt;Task&gt;</c> for InMemory).
/// </summary>
public interface IBatchOperation
{
}
