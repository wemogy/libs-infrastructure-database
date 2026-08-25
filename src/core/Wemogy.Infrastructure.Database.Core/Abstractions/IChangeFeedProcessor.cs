using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     A running reader of the change feed of one repository. Created stopped: nothing is read and
///     no handler is invoked until <see cref="StartAsync"/> is awaited.
///     <para>
///         The processor takes leases on the ranges of the container, so several instances of the
///         same processor name share the work rather than each seeing every change, and the position
///         it reached is checkpointed - a processor restarted under the same name continues where the
///         previous one stopped rather than from the position its options ask for.
///     </para>
/// </summary>
public interface IChangeFeedProcessor : IAsyncDisposable
{
    /// <summary>
    ///     Starts reading. Returns once the processor is running, not once it caught up.
    /// </summary>
    Task StartAsync();

    /// <summary>
    ///     Stops reading and releases the leases, so another instance can pick them up without
    ///     waiting for them to expire. Returns once the handler is no longer invoked. Stopping a
    ///     processor that is not running does nothing.
    /// </summary>
    Task StopAsync();
}
