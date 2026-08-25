using System;
using Wemogy.Infrastructure.Database.Core.Delegates;

namespace Wemogy.Infrastructure.Database.Core.Models;

/// <summary>
///     Tunes a change feed processor. Every value is optional; the defaults are the ones a
///     projection wants.
/// </summary>
public class ChangeFeedProcessorOptions
{
    /// <summary>
    ///     Whether a processor that has no checkpoint yet reads the container from its beginning
    ///     instead of starting at the current end of the feed.
    ///     <para>
    ///         Off by default, so a processor started for the first time against a container that is
    ///         already full does not replay all of it. Turn it on to build a projection from the
    ///         existing documents without a separate backfill. Once a checkpoint exists this has no
    ///         effect - the checkpoint wins, so restarting a processor never replays.
    ///     </para>
    ///     <para>
    ///         Not supported by the all-versions-and-deletes feed, which can only be read from the
    ///         point the processor first started at.
    ///     </para>
    /// </summary>
    public bool StartFromBeginning { get; set; }

    /// <summary>
    ///     Identifies this compute instance among the instances sharing the processor name, and has
    ///     to be unique among them - two instances claiming the same name fight over the same leases.
    ///     Defaults to the machine name, which is unique per pod, container or VM in the usual
    ///     deployment.
    /// </summary>
    public string? InstanceName { get; set; }

    /// <summary>
    ///     The largest number of changes handed to the handler in one batch. Left to the provider by
    ///     default. Lowering it bounds how much work a failing batch repeats, raising it lets a
    ///     projection amortize its own writes over more changes.
    /// </summary>
    public int? MaxItemsPerBatch { get; set; }

    /// <summary>
    ///     How long the processor waits before looking for changes again once the feed is drained.
    ///     Left to the provider by default.
    /// </summary>
    public TimeSpan? PollInterval { get; set; }

    /// <summary>
    ///     Notified when reading or handling a batch failed. Without it, a handler that keeps
    ///     throwing keeps retrying its batch with nothing to show for it.
    /// </summary>
    public ChangeFeedErrorHandler? OnError { get; set; }
}
