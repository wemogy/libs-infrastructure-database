using System;
using System.Text.Json.Serialization;
using Wemogy.Infrastructure.Database.Core.Converters;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class PartitionKeyAttribute : JsonConverterAttribute
{
    private readonly string _prefix = "a_"; // TODO: set this from the IDatabaseTenantProvider

    internal void SetServiceCollection()
    {
    }

    public override JsonConverter? CreateConverter(Type typeToConvert)
    {
        return new PartitionKeyJsonConverter(_prefix);
    }
}
