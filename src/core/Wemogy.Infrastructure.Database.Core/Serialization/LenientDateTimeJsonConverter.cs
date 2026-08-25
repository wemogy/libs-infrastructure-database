using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wemogy.Infrastructure.Database.Core.Serialization;

/// <inheritdoc cref="LenientDateTimeOffsetJsonConverter"/>
internal sealed class LenientDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TryGetDateTime(out var value))
        {
            return value;
        }

        return DateTime.Parse(
            reader.GetString()!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind);
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
