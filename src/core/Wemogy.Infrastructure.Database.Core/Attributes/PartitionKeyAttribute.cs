using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class PartitionKeyAttribute : JsonConverterAttribute
{
    internal void SetServiceCollection()
    {

    }

    public override JsonConverter? CreateConverter(Type typeToConvert)
    {
        return base.CreateConverter(typeToConvert);
    }

    class PartitionKeyJsonConverter : JsonConverter<string>
    {
        public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }
    }
}
