using System;
using Bogus;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

/// <summary>
///     The running allowance of one meter for one customer in one time bucket - a different shape
///     than the <see cref="UsageEvent"/> it shares a partition with. It is the second entity type
///     the mixed-type partition batch exists for: a consume both records an event and moves the
///     balance, and the two writes have to land together or not at all.
///     <para>
///         Its partition key mirrors the one of <see cref="UsageEvent"/> component for component, so
///         a balance and the events that move it fall into the same logical partition, and it is
///         mapped to the same container so a Cosmos batch can span both.
///     </para>
/// </summary>
public class QuotaBalance : EntityBase
{
    [HierarchicalPartitionKey(0)]
    public string CustomerId { get; set; }

    [HierarchicalPartitionKey(1)]
    public string MeterSlug { get; set; }

    [HierarchicalPartitionKey(2)]
    public string TimeBucket { get; set; }

    /// <summary>
    ///     How much of the allowance has been consumed. A decimal in fixed-point encoding, so a
    ///     conditional increment against a cap stays exact under concurrency, which a floating meter
    ///     would not.
    /// </summary>
    [FixedPoint(Scale = 6)]
    public decimal Consumed { get; set; }

    public QuotaBalance()
        : base(Guid.NewGuid().ToString())
    {
        CustomerId = string.Empty;
        MeterSlug = string.Empty;
        TimeBucket = string.Empty;
    }

    public static Faker<QuotaBalance> Faker
    {
        get
        {
            return new Faker<QuotaBalance>()
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
                    x => x.Consumed,
                    f => 0m)
                .RuleFor(
                    x => x.IsDeleted,
                    f => false);
        }
    }

    /// <summary>
    ///     The partition key of this balance, as a caller has to pass it to address the document.
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
