using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Delegates;

/// <summary>
///     Handles one batch of changes read from the latest version change feed.
///     <para>
///         The handler is only invoked with a non-empty batch, and the batch is only checkpointed
///         once the returned task completed. A handler that throws leaves the batch uncheckpointed,
///         so the same changes are delivered again on the next read - which makes the handler's work
///         at-least-once, not exactly-once.
///     </para>
/// </summary>
/// <param name="changes">
///     The changed documents, in the order they were written within
///     <see cref="ChangeFeedContext.RangeId"/>. Each carries the *full* document, whether the write
///     behind it replaced the document or only patched a field of it.
/// </param>
/// <param name="context">The range the batch was read from</param>
/// <param name="cancellationToken">Cancelled when the processor is stopped</param>
public delegate Task ChangeFeedHandler<TEntity>(
    IReadOnlyCollection<TEntity> changes,
    ChangeFeedContext context,
    CancellationToken cancellationToken);
