using Wemogy.Core.Errors;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Errors;

/// <summary>
///     The errors a partial document update can fail with. Both providers throw through this
///     class, so the exception type, the error code and the message do not depend on which
///     provider applied the patch.
/// </summary>
public static class PatchError
{
    /// <summary>
    ///     The condition of the patch did not hold, so nothing was applied.
    ///     <para>
    ///         This is deliberately a conflict and not a precondition failure: every repository is
    ///         wrapped by a retry proxy that retries <see cref="PreconditionFailedErrorException"/>,
    ///         and a failed condition is deterministic - retrying it would burn three attempts and a
    ///         backoff on an answer the first attempt already had. It also says something different:
    ///         a stale eTag means "someone changed this, read again and decide", a failed condition
    ///         means "the state does not permit this".
    ///     </para>
    /// </summary>
    public static ConflictErrorException ConditionNotMet(string id, string partitionKey)
    {
        return Error.Conflict(
            "PatchConditionNotMet",
            $"The patch condition for the entity with id {id} and partition key {partitionKey} did not hold, so no operation was applied");
    }

    /// <summary>
    ///     The condition of a patch inside a transactional batch did not hold, so the whole batch
    ///     was rolled back.
    /// </summary>
    public static ConflictErrorException ConditionNotMet(int operationIndex, string id, string partitionKey)
    {
        return Error.Conflict(
            "PatchConditionNotMet",
            $"Operation {operationIndex} of the transactional batch failed: the patch condition for the entity with id {id} and partition key {partitionKey} did not hold, so the batch was rolled back");
    }

    /// <summary>
    ///     The path of an operation is not a chain of member accesses, e.g. a method call, an
    ///     indexer or a cast to an unrelated type.
    /// </summary>
    public static UnexpectedErrorException PathNotSupported(string path)
    {
        return Error.Unexpected(
            "PatchPathNotSupported",
            $"The patch path {path} is not supported: a path has to be a chain of member accesses, e.g. x => x.Balance or x => x.Inner.Value, and its last member has to be writable");
    }

    /// <summary>
    ///     The path of an operation addresses the id, the partition key or the eTag of the
    ///     document, none of which a caller may patch.
    /// </summary>
    public static UnexpectedErrorException PathNotAllowed(string path, string reason)
    {
        return Error.Unexpected(
            "PatchPathNotAllowed",
            $"The patch path {path} addresses the {reason} of the entity, which cannot be patched");
    }

    /// <summary>
    ///     More operations were added to a patch than a patch can carry.
    /// </summary>
    public static UnexpectedErrorException OperationLimitExceeded(int maxOperationCount)
    {
        return Error.Unexpected(
            "PatchOperationLimitExceeded",
            $"A patch is limited to {maxOperationCount} operations");
    }

    /// <summary>
    ///     A patch without operations. Unlike an empty transactional batch, which a caller can
    ///     reach by looping over an empty collection, an empty patch is always a mistake at the
    ///     call site.
    /// </summary>
    public static UnexpectedErrorException IsEmpty()
    {
        return Error.Unexpected(
            "PatchIsEmpty",
            "A patch has to carry at least one operation");
    }

    /// <summary>
    ///     The condition uses a construct the provider cannot evaluate. The in-memory provider
    ///     compiles conditions in process and therefore accepts more than Cosmos DB, whose LINQ
    ///     provider has to translate the condition into SQL.
    /// </summary>
    public static UnexpectedErrorException ConditionNotSupported(string condition, string? reason = null)
    {
        var hint = string.IsNullOrEmpty(reason) ? string.Empty : $": {reason}";

        return Error.Unexpected(
            "PatchConditionNotSupported",
            $"The patch condition {condition} cannot be translated into a query the database can evaluate{hint}");
    }

    /// <summary>
    ///     The patch failed for a reason that does not map to one of the specific errors above.
    /// </summary>
    public static FailureErrorException Failed(string id, string partitionKey, string reason)
    {
        return Error.Failure(
            "PatchFailed",
            $"The patch of the entity with id {id} and partition key {partitionKey} failed: {reason}");
    }
}
