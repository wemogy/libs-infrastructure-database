using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Delegates;

/// <summary>
///     Handles one batch of changes read from the all-versions-and-deletes change feed. Unlike the
///     latest version feed, this one reports every write separately - including deletes - rather
///     than the current state of the documents that changed.
/// </summary>
/// <param name="changes">
///     The changes, in the order they were written within
///     <see cref="ChangeFeedContext.RangeId"/>
/// </param>
/// <param name="context">The range the batch was read from</param>
/// <param name="cancellationToken">Cancelled when the processor is stopped</param>
public delegate Task AllVersionsAndDeletesChangeFeedHandler<TEntity>(
    IReadOnlyCollection<DatabaseChange<TEntity>> changes,
    ChangeFeedContext context,
    CancellationToken cancellationToken);
