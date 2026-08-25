using System;
using System.Text.Json.Serialization;
using Wemogy.Infrastructure.Database.Core.Serialization;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

/// <summary>
///     Marks a <see cref="DateTimeOffset"/> that was stored as a <see cref="DateTime"/> before,
///     and is therefore in a document without an offset of its own.
/// </summary>
/// <remarks>
///     <para>
///         Applies <see cref="UtcDateTimeOffsetJsonConverter"/>: a stored value carrying no offset
///         is read as UTC rather than in the zone of the reading machine, and a zero offset is
///         written back as the <c>Z</c> form the document was written with.
///     </para>
///     <para>
///         The attribute travels with the property, so it holds wherever the entity is
///         deserialized - not only through the client this library configures, but also through
///         a caller's own <see cref="System.Text.Json.JsonSerializerOptions"/>. An entity that
///         implements <c>IEntityBase</c> directly rather than deriving from <c>EntityBase</c>
///         should carry it on both timestamps for the same reason.
///     </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
public sealed class UtcDateTimeOffsetAttribute : JsonConverterAttribute
{
    public UtcDateTimeOffsetAttribute()
        : base(typeof(UtcDateTimeOffsetJsonConverter))
    {
    }
}
