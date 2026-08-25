using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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
            // ToDo: implement the property type mappings as well
            _fixedPointScales = FixedPointMetadata.GetScalesByPath(modelType);
        }

        public void AddCustomMappings(Dictionary<string, Type> customMappings)
        {
            _propertyTypes.Merge(customMappings);
        }

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
                var deserializedValue = JsonConvert.DeserializeObject(jsonValue);
                if (deserializedValue == null)
                {
                    return null;
                }

                if (_propertyTypes.TryGetValue(
                        propertyPath,
                        out var propertyType))
                {
                    if (propertyType == typeof(DateTime))
                    {
                        if (deserializedValue is long l)
                        {
                            return l.FromUnixEpochDate();
                        }
                    }
                }

                return deserializedValue;
            }
            catch
            {
                Console.WriteLine(
                    $"MappingMetadata.Deserialize: Use fallback for property {propertyPath} with json value {jsonValue}");
                return jsonValue;
            }
        }

        private static object? DeserializeFixedPoint(string propertyPath, string jsonValue, int scale)
        {
            JToken token;

            try
            {
                token = JToken.Parse(jsonValue);
            }
            catch (JsonException)
            {
                throw FixedPointError.FilterValueNotSupported(
                    propertyPath,
                    jsonValue);
            }

            switch (token.Type)
            {
                case JTokenType.Null:
                case JTokenType.Undefined:
                    return null;

                // an "is one of" filter hands the whole array in first and then re-enters this
                // method once per item, which is where the scaling happens
                case JTokenType.Array:
                    return (JArray)token;

                case JTokenType.Integer:
                case JTokenType.Float:
                case JTokenType.String:
                    return ToScaledValue(
                        propertyPath,
                        jsonValue,
                        scale,
                        token);

                default:
                    throw FixedPointError.FilterValueNotSupported(
                        propertyPath,
                        jsonValue);
            }
        }

        private static long ToScaledValue(string propertyPath, string jsonValue, int scale, JToken token)
        {
            decimal value;

            try
            {
                value = token.Type == JTokenType.String
                    ? decimal.Parse(
                        token.Value<string>()!,
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture)
                    : token.Value<decimal>();
            }
            catch (Exception e) when (e is FormatException or OverflowException or InvalidCastException)
            {
                throw FixedPointError.FilterValueNotSupported(
                    propertyPath,
                    jsonValue);
            }

            return FixedPointScale.ToScaled(
                value,
                scale,
                propertyPath);
        }
    }
}
