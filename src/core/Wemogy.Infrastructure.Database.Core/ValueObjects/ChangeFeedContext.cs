namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

/// <summary>
///     Identifies the range of the container a batch of changes was read from.
/// </summary>
public class ChangeFeedContext
{
    public ChangeFeedContext(string rangeId)
    {
        RangeId = rangeId;
    }

    /// <summary>
    ///     The range the changes of this batch came from - a *physical* partition key range, not a
    ///     logical partition key.
    ///     <para>
    ///         This is the unit the feed orders by: changes carrying the same range id arrive in the
    ///         order they were written, changes from two different range ids have no order relative
    ///         to each other. The value is not stable over the lifetime of a container either, since
    ///         a physical range splits as the data in it grows and its id is replaced by the ids of
    ///         the ranges it split into. Treat it as the scope of the ordering guarantee, not as a
    ///         key to persist.
    ///     </para>
    /// </summary>
    public string RangeId { get; }
}
