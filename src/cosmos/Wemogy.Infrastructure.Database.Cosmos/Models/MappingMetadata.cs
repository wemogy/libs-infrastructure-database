using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Cosmos.Models
{
    public class MappingMetadata
    {
        private readonly Dictionary<string, Type> _propertyTypes;

        /// <summary>
        ///     The scale of every fixed-point member of the entity type, by property path. A query
        ///     addresses a property by its serialized, camelCased name, so the lookup ignores case.
        /// </summary>
        private IReadOnlyDictionary<string, int> _fixedPointScales;

        public MappingMetadata()
        {
            _propertyTypes = new Dictionary<string, Type>(StringComparer.CurrentCultureIgnoreCase);
            _fixedPointScales = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }

        public void InitializeUsingReflection(Type modelType)
        {
            InitializeUsingReflection(
                modelType,
                null);
        }

        /// <summary>
        ///     Reads the metadata of the entity type that a query has to know about.
        /// </summary>
        /// <param name="modelType">The entity type of the repository</param>
        /// <param name="serializeMemberName">
        ///     How the client names a member in the document, so a filter on a member renamed with
        ///     a <c>[JsonPropertyName]</c> finds its fixed-point scale under the stored name too
        /// </param>
        public void InitializeUsingReflection(Type modelType, Func<MemberInfo, string>? serializeMemberName)
        {
            // ToDo: implement the property type mappings as well
            _fixedPointScales = FixedPointMetadata.GetScalesByPath(
                modelType,
                serializeMemberName);
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
            // a member marked with [FixedPoint] is stored as the integer value * 10^Scale, so a
            // filter or a search-after cursor has to compare against the scaled value - an
            // unscaled 0.5 against a stored 500000 would answer a different question entirely
            if (_fixedPointScales.TryGetValue(
                    propertyPath,
                    out var scale))
            {
                return DeserializeFixedPoint(
                    propertyPath,
                    jsonValue,
                    scale);
            }

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
            var isFixedPoint = _fixedPointScales.TryGetValue(
                propertyPath,
                out var scale);

            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(jsonValue);
            }
            catch
            {
                if (isFixedPoint)
                {
                    // a fixed-point filter is refused rather than compared unscaled
                    throw FixedPointError.FilterValueNotSupported(
                        propertyPath,
                        jsonValue);
                }

                Console.WriteLine(
                    $"MappingMetadata.DeserializeArray: Use fallback for property {propertyPath} with json value {jsonValue}");
                return null;
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return null;
                }

                var values = new List<object?>();

                foreach (var element in document.RootElement.EnumerateArray())
                {
                    values.Add(
                        isFixedPoint
                            ? MapFixedPoint(
                                propertyPath,
                                element,
                                scale)
                            : Map(
                                propertyPath,
                                element));
                }

                return values;
            }
        }

        private static object? DeserializeFixedPoint(string propertyPath, string jsonValue, int scale)
        {
            JsonDocument document;

            try
            {
                document = JsonDocument.Parse(jsonValue);
            }
            catch (JsonException)
            {
                throw FixedPointError.FilterValueNotSupported(
                    propertyPath,
                    jsonValue);
            }

            using (document)
            {
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Array)
                {
                    // an "is one of" filter needs a parameter per item, each of them scaled
                    var items = new List<object?>();

                    foreach (var element in root.EnumerateArray())
                    {
                        items.Add(
                            MapFixedPoint(
                                propertyPath,
                                element,
                                scale));
                    }

                    return items;
                }

                return MapFixedPoint(
                    propertyPath,
                    root,
                    scale);
            }
        }

        private static object? MapFixedPoint(string propertyPath, JsonElement element, int scale)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                case JsonValueKind.Number:
                case JsonValueKind.String:
                    return ToScaledValue(
                        propertyPath,
                        element,
                        scale);
                default:
                    throw FixedPointError.FilterValueNotSupported(
                        propertyPath,
                        element.GetRawText());
            }
        }

        private static long ToScaledValue(string propertyPath, JsonElement element, int scale)
        {
            decimal value;

            try
            {
                value = element.ValueKind == JsonValueKind.String
                    ? decimal.Parse(
                        element.GetString()!,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture)
                    : element.GetDecimal();
            }
            catch (Exception e) when (e is FormatException or OverflowException or InvalidOperationException)
            {
                throw FixedPointError.FilterValueNotSupported(
                    propertyPath,
                    element.GetRawText());
            }

            return FixedPointScale.ToScaled(
                value,
                scale,
                propertyPath);
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
                    // A timestamp is stored, compared and ordered as the string it was written
                    // as, so a filter value and a search-after cursor only match while they are
                    // spelled the way the document was. Parsing it here hands the value to the
                    // client of the container as a timestamp, which respells a zero offset as the
                    // "Z" form the entity was written with - without this, a cursor built from a
                    // DateTimeOffset arrives as "+00:00", sorts before every stored "Z" and makes
                    // the page boundary repeat a row rather than move past it.
                    if (element.TryGetDateTimeOffset(out var timestamp))
                    {
                        return timestamp;
                    }

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
