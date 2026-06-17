using System;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     Base interface for repository entities.
/// </summary>
public interface IEntityBase
{
    public string Id { get; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>
    ///     The entity's eTag used for optimistic concurrency.
    ///     Populated from the store on read; never persisted into the document body.
    /// </summary>
    public string? ETag { get; }
}
