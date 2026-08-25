using System;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public abstract class EntityBase : IEntityBase
{
    protected EntityBase()
        : this(Guid.NewGuid().ToString())
    {
    }

    protected EntityBase(string id)
    {
        Id = id;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    [SoftDeleteFlag]
    public bool IsDeleted { get; set; }

    [Id]
    public string Id { get; init; }

    [ETag]
    public string? ETag { get; init; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
