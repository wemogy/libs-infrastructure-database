using System;
using System.Linq;
using Microsoft.Azure.Cosmos;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public partial class CosmosDatabaseClient<TEntity>
    {
        public IChangeFeedProcessor CreateChangeFeedProcessor(
            string processorName,
            ChangeFeedHandler<TEntity> onChanges,
            ChangeFeedProcessorOptions? options)
        {
            EnsureProcessorNameIsNotEmpty(processorName);

            // typed rather than a lambda, because the container offers an overload for the older
            // handler without a context and the two are ambiguous for a lambda
            Container.ChangeFeedHandler<TEntity> handler = (context, changes, cancellationToken) => onChanges(
                changes,
                new ChangeFeedContext(context.LeaseToken),
                cancellationToken);

            // the items are deserialized by the serializer of the CosmosClient, which is the same
            // CosmosEntitySerializer every read goes through - so a change carries the entity in
            // exactly the shape GetAsync would return it in, eTag included
            var builder = _container.GetChangeFeedProcessorBuilder(
                processorName,
                handler);

            if (options?.StartFromBeginning == true)
            {
                // WithStartFromBeginning() is internal to the SDK; the smallest possible start time
                // is the documented public equivalent and is what the SDK does internally as well
                builder = builder.WithStartTime(DateTime.MinValue.ToUniversalTime());
            }

            return BuildProcessor(
                builder,
                processorName,
                options);
        }

        public IChangeFeedProcessor CreateAllVersionsAndDeletesChangeFeedProcessor(
            string processorName,
            AllVersionsAndDeletesChangeFeedHandler<TEntity> onChanges,
            ChangeFeedProcessorOptions? options)
        {
            EnsureProcessorNameIsNotEmpty(processorName);

            if (options?.StartFromBeginning == true)
            {
                throw ChangeFeedError.StartFromBeginningNotSupported();
            }

            Container.ChangeFeedHandler<ChangeFeedItem<TEntity>> handler =
                (context, changes, cancellationToken) => onChanges(
                    changes.Select(ToDatabaseChange).ToList(),
                    new ChangeFeedContext(context.LeaseToken),
                    cancellationToken);

            var builder = _container.GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(
                processorName,
                handler);

            return BuildProcessor(
                builder,
                processorName,
                options);
        }

        /// <summary>
        ///     Maps a change of the all-versions-and-deletes feed onto the provider-independent one.
        ///     <para>
        ///         A delete carries no current version - Cosmos reports the document that was removed
        ///         as the previous one instead, which is the only place its contents are still
        ///         available.
        ///     </para>
        /// </summary>
        private static DatabaseChange<TEntity> ToDatabaseChange(ChangeFeedItem<TEntity> item)
        {
            return new DatabaseChange<TEntity>(
                ToDatabaseChangeOperation(item.Metadata.OperationType),
                item.Current,
                item.Previous,
                item.Metadata.IsTimeToLiveExpired);
        }

        private static DatabaseChangeOperation ToDatabaseChangeOperation(ChangeFeedOperationType operationType)
        {
            switch (operationType)
            {
                case ChangeFeedOperationType.Create:
                    return DatabaseChangeOperation.Create;
                case ChangeFeedOperationType.Replace:
                    return DatabaseChangeOperation.Replace;
                case ChangeFeedOperationType.Delete:
                    return DatabaseChangeOperation.Delete;
                default:
                    throw Error.Unexpected(
                        "ChangeFeedOperationTypeNotSupported",
                        $"The change feed operation type {operationType} is not supported");
            }
        }

        private static void EnsureProcessorNameIsNotEmpty(string processorName)
        {
            if (string.IsNullOrWhiteSpace(processorName))
            {
                throw ChangeFeedError.ProcessorNameIsEmpty();
            }
        }

        private IChangeFeedProcessor BuildProcessor(
            ChangeFeedProcessorBuilder builder,
            string processorName,
            ChangeFeedProcessorOptions? options)
        {
            // the machine name is unique per pod, container or VM, which is the granularity the SDK
            // wants: one instance name per process competing for the leases
            builder = builder
                .WithInstanceName(options?.InstanceName ?? Environment.MachineName)
                .WithLeaseContainer(_leaseContainer);

            if (options?.MaxItemsPerBatch != null)
            {
                builder = builder.WithMaxItems(options.MaxItemsPerBatch.Value);
            }

            if (options?.PollInterval != null)
            {
                builder = builder.WithPollInterval(options.PollInterval.Value);
            }

            if (options?.OnError != null)
            {
                var onError = options.OnError;
                builder = builder.WithErrorNotification(
                    (leaseToken, exception) => onError(
                        new ChangeFeedContext(leaseToken),
                        exception));
            }

            return new CosmosChangeFeedProcessor(
                builder.Build(),
                processorName,
                _options.DatabaseName,
                _options.ContainerName ?? string.Empty,
                _options.LeaseContainerName);
        }
    }
}
