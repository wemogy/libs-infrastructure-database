using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wemogy.Infrastructure.Database.Core.Converters;

public class PartitionKeyJsonConverter : JsonConverter<string>
{
    private readonly string _prefix;

    public PartitionKeyJsonConverter(string prefix)
    {
        _prefix = prefix;
    }

    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var value = reader.GetString()!.TrimStart(_prefix.ToCharArray());
        return value;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue($"a_{value}");
    }
}
