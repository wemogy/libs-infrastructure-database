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
                var tenantChanges = FilterToTenant(changes);

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
                var tenantChanges = FilterToTenant(changes);

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
    ///     Keeps the documents that live in the partition prefix of the current tenant and strips
    ///     the prefix off them, so a handler sees the partition key values it wrote.
    /// </summary>
    /// <param name="changes">The batch as the underlying repository read it, across all tenants</param>
    private List<TEntity> FilterToTenant(IReadOnlyCollection<TEntity> changes)
    {
        var prefix = BuildComposedPartitionKeyComponent(null);
        var tenantChanges = new List<TEntity>(changes.Count);

        foreach (var entity in changes)
        {
            if (!BelongsToTenant(entity, prefix))
            {
                continue;
            }

            RemovePartitionKeyPrefix(entity);
            tenantChanges.Add(entity);
        }

        return tenantChanges;
    }

    /// <summary>
    ///     Keeps the changes whose document lives in the partition prefix of the current tenant and
    ///     strips the prefix off *both* versions it carries.
    /// </summary>
    /// <param name="changes">The batch as the underlying repository read it, across all tenants</param>
    private List<DatabaseChange<TEntity>> FilterToTenant(IReadOnlyCollection<DatabaseChange<TEntity>> changes)
    {
        var prefix = BuildComposedPartitionKeyComponent(null);
        var tenantChanges = new List<DatabaseChange<TEntity>>(changes.Count);

        foreach (var change in changes)
        {
            // a delete carries no current version, so the partition of the document that was removed
            // is the one on the previous version
            var entity = change.Current ?? change.Previous;

            if (entity is null || !BelongsToTenant(entity, prefix))
            {
                continue;
            }

            // both versions, not just the one the tenancy was judged on: a handler comparing the
            // previous against the current version must not find the prefix on one and not the other
            if (change.Current is not null)
            {
                RemovePartitionKeyPrefix(change.Current);
            }

            if (change.Previous is not null)
            {
                RemovePartitionKeyPrefix(change.Previous);
            }

            tenantChanges.Add(change);
        }

        return tenantChanges;
    }

    private bool BelongsToTenant(TEntity entity, string prefix)
    {
        var partitionKey = (string)_partitionKeyProperty.GetValue(entity)!;
        return partitionKey.StartsWith(
            prefix,
            StringComparison.Ordinal);
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

        return BuildComposedPartitionKeyComponent(processorName);
    }
}
