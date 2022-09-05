using System;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Constants;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class GlobalEntityBase<TId> : EntityBase<TId>
    where TId : IEquatable<TId>
{
    [PartitionKey]
    public string PartitionKey { get; set; } = PartitionKeyDefaults.GlobalPartition;

    protected GlobalEntityBase(TId id)
        : base(id)
    {
    }
}
