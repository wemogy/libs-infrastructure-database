namespace Wemogy.Infrastructure.Database.Core.Enums;

/// <summary>
///     The write a change on the feed came from.
///     <para>
///         Only reported by the all-versions-and-deletes feed. The latest version feed carries the
///         document as it is now and does not say which write produced it.
///     </para>
/// </summary>
public enum DatabaseChangeOperation
{
    /// <summary>
    ///     The document was inserted.
    /// </summary>
    Create,

    /// <summary>
    ///     The document was overwritten or partially updated.
    /// </summary>
    Replace,

    /// <summary>
    ///     The document was removed, either by a delete or by its time to live expiring.
    /// </summary>
    Delete
}
