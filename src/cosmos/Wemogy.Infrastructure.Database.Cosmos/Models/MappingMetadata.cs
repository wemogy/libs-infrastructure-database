using System;
using System.Collections.Generic;
using System.Text.Json;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Cosmos.Models
{
    public class MappingMetadata
    {
        private readonly Dictionary<string, Type> _propertyTypes;

        public MappingMetadata()
        {
            _propertyTypes = new Dictionary<string, Type>(StringComparer.CurrentCultureIgnoreCase);
        }

        public void InitializeUsingReflection(Type modelType)
        {
            // ToDo: implement somehow
        }

        public void AddCustomMappings(Dictionary<string, Type> customMappings)
        {
            _propertyTypes.Merge(customMappings);
        }

        /// <summary>
        ///     Turns the JSON of a filter value into the CLR value that goes into the query as a
        ///     parameter.
        /// </summary>
        public object? Deserialize(string propertyPath, string jsonValue)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonValue);
                return Map(
                    propertyPath,
                    document.RootElement);
            }
            catch
            {
                Console.WriteLine(
                    $"MappingMetadata.Deserialize: Use fallback for property {propertyPath} with json value {jsonValue}");
                return jsonValue;
            }
        }

        /// <summary>
        ///     Turns the JSON of a filter value into one CLR value per element, or null when the
        ///     JSON is not an array.
        /// </summary>
        /// <remarks>
        ///     A comparator like <c>IsOneOf</c> needs a parameter per element rather than one
        ///     parameter holding the whole array.
        /// </remarks>
        public List<object?>? DeserializeArray(string propertyPath, string jsonValue)
        {
            try
            {
                using var document = JsonDocument.Parse(jsonValue);
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var values = new List<object?>();
                foreach (var element in document.RootElement.EnumerateArray())
                {
                    values.Add(
                        Map(
                            propertyPath,
                            element));
                }

                return values;
            }
            catch
            {
                Console.WriteLine(
                    $"MappingMetadata.DeserializeArray: Use fallback for property {propertyPath} with json value {jsonValue}");
                return null;
            }
        }

        /// <summary>
        ///     Maps one JSON element onto the CLR value a Cosmos query parameter is written from.
        /// </summary>
        /// <remarks>
        ///     A parameter is serialized by the client of the container, so the value has to be a
        ///     plain CLR value rather than a node of the document it was parsed from.
        /// </remarks>
        private object? Map(string propertyPath, JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.Number:
                    return MapNumber(
                        propertyPath,
                        element);
                case JsonValueKind.True:
                case JsonValueKind.False:
                    return element.GetBoolean();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.Array:
                    var items = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        items.Add(
                            Map(
                                propertyPath,
                                item));
                    }

                    return items;
                default:
                    // an object goes into the query the way it was written
                    return JsonSerializer.Deserialize<Dictionary<string, object?>>(element.GetRawText());
            }
        }

        private object? MapNumber(string propertyPath, JsonElement element)
        {
            if (element.TryGetInt64(out var longValue))
            {
                if (_propertyTypes.TryGetValue(
                        propertyPath,
                        out var propertyType) && propertyType == typeof(DateTime))
                {
                    return longValue.FromUnixEpochDate();
                }

                return longValue;
            }

            return element.GetDouble();
        }
    }
}
