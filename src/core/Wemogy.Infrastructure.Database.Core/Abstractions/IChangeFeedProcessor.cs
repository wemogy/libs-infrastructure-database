using System;
using System.Threading.Tasks;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     A running reader of the change feed of one repository. Created stopped: nothing is read and
///     no handler is invoked until <see cref="StartAsync"/> is awaited.
///     <para>
///         The position the processor reached is checkpointed, so one restarted under the same name
///         continues where the previous one stopped rather than from the position its options ask
///         for - it neither replays what it handled nor skips what was written while it was down.
///     </para>
///     <para>
///         Instances sharing a processor name take leases on the ranges of the container and split
///         them between them, so a deployment scaled to several replicas handles each change once.
///         The in-memory provider models the checkpointing but not the lease contention: two of its
///         processors running under one name each see every change.
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
