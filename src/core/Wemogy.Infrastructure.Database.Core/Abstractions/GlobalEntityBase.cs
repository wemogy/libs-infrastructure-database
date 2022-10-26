using System;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Constants;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class GlobalEntityBase : EntityBase
{
    protected GlobalEntityBase()
        : base(Guid.NewGuid().ToString())
    {
    }

    [PartitionKey]
    public string PartitionKey { get; set; } = PartitionKeyDefaults.GlobalPartition;
}
