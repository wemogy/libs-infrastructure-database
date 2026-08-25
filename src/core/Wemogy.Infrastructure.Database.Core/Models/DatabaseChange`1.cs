using Wemogy.Infrastructure.Database.Core.Enums;

namespace Wemogy.Infrastructure.Database.Core.Models;

/// <summary>
///     One change on the all-versions-and-deletes feed: the write that happened, the document as it
///     is after that write, and - where the provider retained it - the document as it was before.
/// </summary>
/// <typeparam name="TEntity">The entity type of the repository the feed reads from</typeparam>
public class DatabaseChange<TEntity>
{
    public DatabaseChange(
        DatabaseChangeOperation operation,
        TEntity? current,
        TEntity? previous,
        bool isTimeToLiveExpired = false)
    {
        Operation = operation;
        Current = current;
        Previous = previous;
        IsTimeToLiveExpired = isTimeToLiveExpired;
    }

    /// <summary>
    ///     The write this change came from.
    /// </summary>
    public DatabaseChangeOperation Operation { get; }

    /// <summary>
    ///     The document after the write, or <c>null</c> for a
    ///     <see cref="DatabaseChangeOperation.Delete"/> - there is no document left to carry.
    /// </summary>
    public TEntity? Current { get; }

    /// <summary>
    ///     The document as it was before the write, or <c>null</c> for a
    ///     <see cref="DatabaseChangeOperation.Create"/>.
    ///     <para>
    ///         For a delete this is the document that was removed, which is the only place its
    ///         contents are still available.
    ///     </para>
    ///     <para>
    ///         Only meaningful for a container that retains previous versions. Cosmos DB sends an
    ///         empty object rather than nothing for a version it does not carry, and an entity that
    ///         fills in its own id - as <see cref="Abstractions.EntityBase"/> does - is
    ///         indistinguishable from a real document once deserialized. The provider normalizes the
    ///         version the operation rules out, so a create never carries a previous one and a delete
    ///         never a current one; what it cannot do is tell an unretained previous version of a
    ///         *replace* from a real one. In practice Cosmos DB serves this feed only for a container
    ///         with a full fidelity retention window, so a replace on a container that reaches this
    ///         code has one.
    ///     </para>
    /// </summary>
    public TEntity? Previous { get; }

    /// <summary>
    ///     Whether this delete was the time to live of the document expiring rather than an explicit
    ///     delete. Always <c>false</c> for the other operations.
    /// </summary>
    public bool IsTimeToLiveExpired { get; }
}
