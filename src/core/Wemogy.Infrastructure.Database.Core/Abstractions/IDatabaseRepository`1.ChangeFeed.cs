using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public partial interface IDatabaseRepository<TEntity>
{
    /// <summary>
    ///     Creates a processor that reads the change feed of this repository and hands every batch
    ///     of changed documents to <paramref name="onChanges"/>. Nothing is read until the returned
    ///     processor is started.
    ///     <para>
    ///         This is the *latest version* feed: it carries the document as it is now, so two writes
    ///         to the same document between two reads arrive as one change carrying the second one.
    ///         Whatever the write was, the change carries the *whole* document - a
    ///         <see cref="PatchAsync(string,ValueObjects.PartitionKeyValue,System.Action{IPatchOperations{TEntity}},System.Linq.Expressions.Expression{System.Func{TEntity,bool}},System.Threading.CancellationToken)"/>
    ///         that touched one field arrives no differently from a replace, which is what lets a
    ///         projection rebuild itself from a change without knowing how the document got there.
    ///         Hard deletes are *not* on this feed; a soft delete is, since it is a write like any
    ///         other.
    ///     </para>
    ///     <para>
    ///         Changes are ordered within a <see cref="ValueObjects.ChangeFeedContext.RangeId"/>,
    ///         which is a physical partition key range and not a logical partition key. There is no
    ///         order between two ranges, and the ranges of a container change as it grows.
    ///     </para>
    /// </summary>
    /// <param name="processorName">
    ///     Identifies the leases and the checkpoint of this processor. Instances sharing this name
    ///     split the ranges between them; a different name reads the same feed independently, from
    ///     its own position.
    /// </param>
    /// <param name="onChanges">Invoked with each non-empty batch of changed documents</param>
    /// <param name="options">Start position, batch size, poll interval and error notification</param>
    /// <returns>The processor, stopped</returns>
    IChangeFeedProcessor CreateChangeFeedProcessor(
        string processorName,
        ChangeFeedHandler<TEntity> onChanges,
        ChangeFeedProcessorOptions? options = null);

    /// <summary>
    ///     Creates a processor that reads the *all versions and deletes* change feed of this
    ///     repository: every write separately rather than the current state of what changed, deletes
    ///     included, and with the previous version of the document attached where the provider
    ///     retained it.
    ///     <para>
    ///         This is what an event log wants and a state projection does not: a document written
    ///         twice between two reads arrives as two changes, and a hard delete arrives as a
    ///         <see cref="Enums.DatabaseChangeOperation.Delete"/> carrying the document that was
    ///         removed. It costs more than the latest version feed and only reaches back as far as
    ///         the retention window of the container, so
    ///         <see cref="ChangeFeedProcessorOptions.StartFromBeginning"/> is refused here.
    ///     </para>
    ///     <para>
    ///         Azure Cosmos DB only serves this feed for a container configured with a full fidelity
    ///         retention window; a container without one delivers no changes at all rather than
    ///         failing.
    ///     </para>
    /// </summary>
    /// <param name="processorName">
    ///     Identifies the leases and the checkpoint of this processor
    /// </param>
    /// <param name="onChanges">Invoked with each non-empty batch of changes</param>
    /// <param name="options">Batch size, poll interval and error notification</param>
    /// <returns>The processor, stopped</returns>
    IChangeFeedProcessor CreateAllVersionsAndDeletesChangeFeedProcessor(
        string processorName,
        AllVersionsAndDeletesChangeFeedHandler<TEntity> onChanges,
        ChangeFeedProcessorOptions? options = null);
}
