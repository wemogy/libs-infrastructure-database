using System;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     Base interface for repository entities.
/// </summary>
public interface IEntityBase
{
    public string Id { get; }

    public string? ETag { get; }

    /// <summary>
    ///     When the entity was created. Always UTC, always written by the library.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    ///     When the entity was last written. Always UTC, always written by the library.
    /// </summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
