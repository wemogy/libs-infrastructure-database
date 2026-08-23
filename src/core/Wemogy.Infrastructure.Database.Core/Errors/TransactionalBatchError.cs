using Wemogy.Core.Errors;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Errors;

/// <summary>
///     The errors a transactional batch can fail with. Both providers throw through this class,
///     so the exception type, the error code and the message of a failed batch do not depend on
///     which provider ran it.
/// </summary>
public static class TransactionalBatchError
{
    /// <summary>
    ///     An entity was added to a batch of another logical partition. Thrown when the
    ///     operation is added, so the stack trace points at the offending call.
    /// </summary>
    public static UnexpectedErrorException PartitionKeyMismatch(
        string entityPartitionKey,
        string batchPartitionKey,
        string entityTypeName)
    {
        return Error.Unexpected(
            "PartitionKeyMismatch",
            $"The partition key {entityPartitionKey} of the entity ({entityTypeName}) does not match the partition key {batchPartitionKey} of the transactional batch, which is limited to a single logical partition");
    }

    /// <summary>
    ///     More operations were added to a batch than a transactional batch can carry.
    /// </summary>
    public static UnexpectedErrorException OperationLimitExceeded(int maxOperationCount)
    {
        return Error.Unexpected(
            "TransactionalBatchOperationLimitExceeded",
            $"A transactional batch is limited to {maxOperationCount} operations");
    }

    /// <summary>
    ///     A batch was executed twice, or an operation was added to it after it had been executed.
    ///     A batch is single-use: its providers consume the recorded operations, so a second
    ///     execution would replay every write on one provider and silently do nothing on another.
    /// </summary>
    public static UnexpectedErrorException AlreadyExecuted()
    {
        return Error.Unexpected(
            "TransactionalBatchAlreadyExecuted",
            "A transactional batch is single-use and has already been executed. Build a new batch instead of reusing this one");
    }

    /// <summary>
    ///     A <c>Create</c> operation of the batch addressed an id that already exists.
    /// </summary>
    public static ConflictErrorException AlreadyExists(int operationIndex, string id)
    {
        return Error.Conflict(
            "AlreadyExists",
            $"Operation {operationIndex} of the transactional batch failed: entity with id {id} already exists");
    }

    /// <summary>
    ///     A <c>Replace</c> or <c>Delete</c> operation of the batch addressed a missing entity.
    /// </summary>
    public static NotFoundErrorException EntityNotFound(
        int operationIndex,
        string id,
        string partitionKey,
        string entityTypeName)
    {
        return DatabaseError.EntityNotFound(
            id,
            partitionKey,
            $"{entityTypeName}, transactional batch operation {operationIndex}");
    }

    /// <summary>
    ///     A <c>Replace</c> operation of the batch carried an eTag that no longer matches the
    ///     version in the database.
    /// </summary>
    public static PreconditionFailedErrorException ETagMismatch(int operationIndex, string id, string partitionKey)
    {
        return Error.PreconditionFailed(
            "EtagMismatch",
            $"Operation {operationIndex} of the transactional batch failed: the eTag of the entity with id {id} and partition key {partitionKey} does not match the version in the database");
    }

    /// <summary>
    ///     The batch failed for a reason that does not map to one of the specific errors above.
    /// </summary>
    public static FailureErrorException Failed(int operationIndex, int statusCode)
    {
        return Error.Failure(
            "TransactionalBatchFailed",
            $"Operation {operationIndex} of the transactional batch failed with status code {statusCode}");
    }

    /// <summary>
    ///     The batch failed without a single operation to blame, so only the batch-level status
    ///     code is known.
    /// </summary>
    public static FailureErrorException Failed(int statusCode)
    {
        return Error.Failure(
            "TransactionalBatchFailed",
            $"The transactional batch failed with status code {statusCode}");
    }
}
