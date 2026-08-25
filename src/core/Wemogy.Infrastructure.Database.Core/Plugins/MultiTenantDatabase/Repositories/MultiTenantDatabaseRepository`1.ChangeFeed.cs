using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Delegates;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;

public partial class MultiTenantDatabaseRepository<TEntity>
{
    public IChangeFeedProcessor CreateChangeFeedProcessor(
        string processorName,
        ChangeFeedHandler<TEntity> onChanges,
        ChangeFeedProcessorOptions? options = null)
    {
        return _databaseRepository.CreateChangeFeedProcessor(
            BuildComposedProcessorName(processorName),
            (changes, context, cancellationToken) =>
            {
                var tenantChanges = FilterToTenant(
                    changes,
                    entity => entity);

                // the underlying feed carries every tenant, so a batch can be empty after filtering.
                // Handlers are documented to only see non-empty batches, and one that writes a
                // projection per batch should not be woken up for another tenant's writes
                return tenantChanges.Count == 0
                    ? Task.CompletedTask
                    : onChanges(
                        tenantChanges,
                        context,
                        cancellationToken);
            },
            options);
    }

    public IChangeFeedProcessor CreateAllVersionsAndDeletesChangeFeedProcessor(
        string processorName,
        AllVersionsAndDeletesChangeFeedHandler<TEntity> onChanges,
        ChangeFeedProcessorOptions? options = null)
    {
        return _databaseRepository.CreateAllVersionsAndDeletesChangeFeedProcessor(
            BuildComposedProcessorName(processorName),
            (changes, context, cancellationToken) =>
            {
                // a delete carries no current version, so the partition key of the document that was
                // removed is the one on the previous version
                var tenantChanges = FilterToTenant(
                    changes,
                    change => change.Current ?? change.Previous);

                return tenantChanges.Count == 0
                    ? Task.CompletedTask
                    : onChanges(
                        tenantChanges,
                        context,
                        cancellationToken);
            },
            options);
    }

    /// <summary>
    ///     Keeps the changes whose document lives in the partition prefix of the current tenant and
    ///     strips the prefix off them, so a handler sees the partition key values it wrote.
    /// </summary>
    /// <param name="changes">The batch as the underlying repository read it, across all tenants</param>
    /// <param name="entitySelector">
    ///     Finds the document to judge inside a change, which is the change itself on the latest
    ///     version feed and the current or previous version on the all-versions feed
    /// </param>
    private List<TChange> FilterToTenant<TChange>(
        IReadOnlyCollection<TChange> changes,
        Func<TChange, TEntity?> entitySelector)
    {
        var prefix = BuildComposedPartitionKey(null);
        var tenantChanges = new List<TChange>(changes.Count);

        foreach (var change in changes)
        {
            var entity = entitySelector(change);
            if (entity is null)
            {
                continue;
            }

            var partitionKey = (string)_partitionKeyProperty.GetValue(entity)!;
            if (!partitionKey.StartsWith(
                    prefix,
                    StringComparison.Ordinal))
            {
                continue;
            }

            RemovePartitionKeyPrefix(entity);
            tenantChanges.Add(change);
        }

        return tenantChanges;
    }

    /// <summary>
    ///     Prefixes the processor name with the tenant, the way every partition key is prefixed.
    ///     <para>
    ///         Checked for emptiness here rather than left to the provider: by the time the provider
    ///         sees the name it carries the tenant prefix and is no longer empty, so an empty name
    ///         would silently become the tenant's own.
    ///     </para>
    ///     <para>
    ///         Without this, the processors of two tenants would share one set of leases: they would
    ///         split the ranges of the container between them and each would then drop everything the
    ///         other tenant's ranges carried, so each tenant would silently see only part of its own
    ///         changes. A name per tenant costs one lease set per tenant and keeps every tenant
    ///         reading the whole feed.
    ///     </para>
    /// </summary>
    private string BuildComposedProcessorName(string processorName)
    {
        if (string.IsNullOrWhiteSpace(processorName))
        {
            throw ChangeFeedError.ProcessorNameIsEmpty();
        }

        return BuildComposedPartitionKey(processorName);
    }
}
