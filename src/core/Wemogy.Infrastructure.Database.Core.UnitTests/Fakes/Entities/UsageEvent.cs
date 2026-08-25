using System;
using Bogus;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

/// <summary>
///     An entity partitioned by a hierarchy of three values, modelled after the metered-usage case
///     hierarchical keys exist for: a customer's traffic grows without bound, so partitioning by
///     the customer alone runs into the per-partition size and throughput ceilings, while a batch
///     still has to stay inside one logical partition.
/// </summary>
public class UsageEvent : EntityBase
{
    [HierarchicalPartitionKey(0)]
    public string CustomerId { get; set; }

    [HierarchicalPartitionKey(1)]
    public string MeterSlug { get; set; }

    [HierarchicalPartitionKey(2)]
    public string TimeBucket { get; set; }

    public long Quantity { get; set; }

    public UsageEvent()
        : base(Guid.NewGuid().ToString())
    {
        CustomerId = string.Empty;
        MeterSlug = string.Empty;
        TimeBucket = string.Empty;
    }

    public static Faker<UsageEvent> Faker
    {
        get
        {
            return new Faker<UsageEvent>()
                .RuleFor(
                    x => x.CustomerId,
                    f => f.Random.Guid().ToString())
                .RuleFor(
                    x => x.MeterSlug,
                    f => f.PickRandom("api-calls", "storage-gb", "seats"))
                .RuleFor(
                    x => x.TimeBucket,
                    f => f.Date.Past().ToString("yyyy-MM"))
                .RuleFor(
                    x => x.Quantity,
                    f => f.Random.Long(1, 1000))
                .RuleFor(
                    x => x.IsDeleted,
                    f => false);
        }
    }

    /// <summary>
    ///     The partition key of this event, as a caller has to pass it to address the document.
    ///     A method rather than a property, so it is not written into the document.
    /// </summary>
    public PartitionKeyValue GetPartitionKey()
    {
        return new PartitionKeyValue(
            CustomerId,
            MeterSlug,
            TimeBucket);
    }
}
