using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wemogy.Infrastructure.Database.Core.Serialization;

/// <summary>
///     Reads a date the way Newtonsoft.Json did, rather than the way System.Text.Json does.
/// </summary>
/// <remarks>
///     System.Text.Json accepts the ISO-8601-1:2019 extended profile and nothing else, while
///     Newtonsoft.Json fell back to <see cref="DateTime.Parse(string, IFormatProvider)"/> and
///     therefore accepted spellings like <c>2026-08-25 10:00:00</c> or <c>08/25/2026</c>. Both
///     turn up in filter values a caller wrote by hand, so a filter that worked before would
///     start throwing out of query building - which has no try/catch around it.
/// </remarks>
internal sealed class LenientDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TryGetDateTimeOffset(out var value))
        {
            return value;
        }

        return DateTimeOffset.Parse(
            reader.GetString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
