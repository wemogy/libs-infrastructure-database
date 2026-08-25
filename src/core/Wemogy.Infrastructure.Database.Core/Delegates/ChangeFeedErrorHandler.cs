using System;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Delegates;

/// <summary>
///     Notified when reading or handling a batch of changes failed. The processor keeps running:
///     an uncheckpointed batch is read again on the next poll, so this is where a caller learns
///     about a handler that keeps failing instead of silently retrying forever.
/// </summary>
/// <param name="context">The range the failing batch was read from</param>
/// <param name="exception">The failure, as thrown by the handler or by the provider</param>
public delegate Task ChangeFeedErrorHandler(ChangeFeedContext context, Exception exception);
