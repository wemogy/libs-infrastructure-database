using System;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class EntityBase : IEntityBase
{
    protected EntityBase(string id)
    {
        Id = id;
        CreatedAt = DateTime.UtcNow;
        UpdatedAt = DateTime.UtcNow;
    }

    [SoftDeleteFlag]
    public bool IsDeleted { get; set; }

    [Id]
    public string Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
