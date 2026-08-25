using System;
using Wemogy.Core.Errors;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Errors;

/// <summary>
///     The errors a change feed processor can fail with. Both providers throw through this class,
///     so the exception type, the error code and the message do not depend on which provider reads
///     the feed.
/// </summary>
public static class ChangeFeedError
{
    /// <summary>
    ///     A processor was created without a name. The name is what the leases and the checkpoints
    ///     are filed under, so it cannot be defaulted.
    /// </summary>
    public static UnexpectedErrorException ProcessorNameIsEmpty()
    {
        return Error.Unexpected(
            "ChangeFeedProcessorNameIsEmpty",
            "A change feed processor needs a name, which identifies the leases and the checkpoints it owns");
    }

    /// <summary>
    ///     <see cref="Models.ChangeFeedProcessorOptions.StartFromBeginning"/> was asked for on an
    ///     all-versions-and-deletes feed, which cannot be read from a position before the one the
    ///     processor first started at - the previous versions and the deletes only exist inside the
    ///     retention window of the container.
    /// </summary>
    public static UnexpectedErrorException StartFromBeginningNotSupported()
    {
        return Error.Unexpected(
            "ChangeFeedStartFromBeginningNotSupported",
            "The all-versions-and-deletes change feed cannot be started from the beginning of the container, because previous versions and deletes are only retained inside the retention window. Read it from the point the processor starts, or use the latest version feed to replay the current documents");
    }

    /// <summary>
    ///     The processor is already running. Starting it twice would take a second set of leases
    ///     under the same instance name.
    /// </summary>
    public static UnexpectedErrorException AlreadyStarted(string processorName)
    {
        return Error.Unexpected(
            "ChangeFeedProcessorAlreadyStarted",
            $"The change feed processor {processorName} is already running");
    }

    /// <summary>
    ///     Neither the monitored container nor the lease container was found when the processor
    ///     started. The lease container is the usual culprit: unlike the monitored one it is not
    ///     created by writing to it, and the provider does not create it either.
    /// </summary>
    public static UnexpectedErrorException ContainerNotFound(
        string databaseName,
        string containerName,
        string leaseContainerName,
        Exception? innerException = null)
    {
        return Error.Unexpected(
            "ChangeFeedContainerNotFound",
            $"The change feed processor could not be started, because the container {containerName} or the lease container {leaseContainerName} does not exist in the database {databaseName}. The lease container has to exist with the partition key path /id before a processor is started",
            innerException);
    }
}
