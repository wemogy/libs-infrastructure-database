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
    ///     When the entity was created. Stamped in UTC by <c>EntityBase</c>'s constructor.
    /// </summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    ///     When the entity was last written.
    /// </summary>
    /// <remarks>
    ///     Stamped in UTC alongside <see cref="CreatedAt"/> when the entity is constructed, and
    ///     <b>not</b> refreshed by any write path: a caller that wants it to track the last write
    ///     has to assign it. Reading it as a last-write timestamp without doing so returns the
    ///     creation timestamp forever.
    /// </remarks>
    public DateTimeOffset UpdatedAt { get; set; }
}
