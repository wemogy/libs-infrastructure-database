using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

[assembly: InternalsVisibleTo("Wemogy.Infrastructure.Database.Cosmos.UnitTests")]

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public partial class CosmosDatabaseClient<TEntity>
    {
        public IChangeFeedProcessor CreateChangeFeedProcessor(
            string processorName,
            ChangeFeedHandler<TEntity> onChanges,
            ChangeFeedProcessorOptions? options)
        {
            EnsureOptionsAreValid(processorName, options);

            // typed rather than a lambda, because the container offers an overload for the older
            // handler without a context and the two are ambiguous for a lambda
            Container.ChangeFeedHandler<TEntity> handler = async (context, changes, cancellationToken) =>
            {
                var feedContext = new ChangeFeedContext(context.LeaseToken);

                foreach (var batch in Batch(changes, options?.MaxItemsPerBatch))
                {
                    await onChanges(
                        batch,
                        feedContext,
                        cancellationToken);
                }
            };

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
            EnsureOptionsAreValid(processorName, options);

            if (options?.StartFromBeginning == true)
            {
                throw ChangeFeedError.StartFromBeginningNotSupported();
            }

            Container.ChangeFeedHandler<ChangeFeedItem<TEntity>> handler =
                async (context, changes, cancellationToken) =>
                {
                    var feedContext = new ChangeFeedContext(context.LeaseToken);
                    var databaseChanges = changes.Select(ToDatabaseChange).ToList();

                    foreach (var batch in Batch(databaseChanges, options?.MaxItemsPerBatch))
                    {
                        await onChanges(
                            batch,
                            feedContext,
                            cancellationToken);
                    }
                };

            var builder = _container.GetChangeFeedProcessorBuilderWithAllVersionsAndDeletes(
                processorName,
                handler);

            return BuildProcessor(
                builder,
                processorName,
                options);
        }

        /// <summary>
        ///     Splits a batch the provider read into the batches the handler is invoked with, so
        ///     <see cref="ChangeFeedProcessorOptions.MaxItemsPerBatch"/> is a bound a caller can rely
        ///     on rather than a hint.
        ///     <para>
        ///         <c>WithMaxItems</c> alone would not do: the Cosmos SDK treats it as a page size
        ///         hint and may hand over more, notably to keep the writes of one transactional batch
        ///         together. Both are set, so the reading stays efficient and the handler still sees
        ///         the bound the in-memory provider enforces.
        ///     </para>
        /// </summary>
        internal static IEnumerable<IReadOnlyCollection<TChange>> Batch<TChange>(
            IReadOnlyCollection<TChange> changes,
            int? maxItemsPerBatch)
        {
            if (maxItemsPerBatch == null || changes.Count <= maxItemsPerBatch.Value)
            {
                if (changes.Count > 0)
                {
                    yield return changes;
                }

                yield break;
            }

            for (var offset = 0; offset < changes.Count; offset += maxItemsPerBatch.Value)
            {
                yield return changes
                    .Skip(offset)
                    .Take(maxItemsPerBatch.Value)
                    .ToList();
            }
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

        private static void EnsureOptionsAreValid(string processorName, ChangeFeedProcessorOptions? options)
        {
            if (string.IsNullOrWhiteSpace(processorName))
            {
                throw ChangeFeedError.ProcessorNameIsEmpty();
            }

            if (options?.MaxItemsPerBatch <= 0)
            {
                throw ChangeFeedError.MaxItemsPerBatchIsNotPositive(options.MaxItemsPerBatch!.Value);
            }
        }

        /// <summary>
        ///     Maps a change of the all-versions-and-deletes feed onto the provider-independent one.
        ///     <para>
        ///         A version the feed does not carry arrives as an *empty object* rather than as
        ///         nothing: <c>ChangeFeedItem&lt;T&gt;.Current</c> is a non-nullable <c>T</c>, so the
        ///         <c>"current": {}</c> of a delete deserializes into a default-constructed entity.
        ///         Forwarding that would break the promise that a delete carries no current version,
        ///         and would have the multi-tenant wrapper judge the tenant on an empty document
        ///         instead of on the one that was actually removed. The operation type is what says
        ///         which version is real, so it is what the two are normalized against.
        ///     </para>
        /// </summary>
        internal DatabaseChange<TEntity> ToDatabaseChange(ChangeFeedItem<TEntity> item)
        {
            var operation = ToDatabaseChangeOperation(item.Metadata.OperationType);

            return new DatabaseChange<TEntity>(
                operation,
                operation == DatabaseChangeOperation.Delete ? null : item.Current,
                operation == DatabaseChangeOperation.Create ? null : item.Previous,
                item.Metadata.IsTimeToLiveExpired);
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
