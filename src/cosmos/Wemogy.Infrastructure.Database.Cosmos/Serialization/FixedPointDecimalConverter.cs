using System;
using System.Globalization;
using Newtonsoft.Json;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     Reads and writes a member marked with the <see cref="FixedPointAttribute"/> as the
    ///     integer <c>value * 10^Scale</c>, e.g. <c>0.5m</c> at scale 6 as <c>500000</c>.
    ///     <para>
    ///         This is the whole point of the attribute: an integer in that range is exact in the
    ///         IEEE 754 binary64 number type of Cosmos DB, so a server-side <c>incr</c> of it is
    ///         exact as well - which a decimal written as a floating point number would not be.
    ///         Read and write go through <see cref="FixedPointScale"/>, so they cannot scale
    ///         differently.
    ///     </para>
    /// </summary>
    internal class FixedPointDecimalConverter : JsonConverter
    {
        private readonly int _scale;
        private readonly string _path;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FixedPointDecimalConverter"/> class.
        /// </summary>
        /// <param name="scale">The declared scale of the member</param>
        /// <param name="path">The member the converter belongs to, for the error messages</param>
        public FixedPointDecimalConverter(int scale, string path)
        {
            _scale = scale;
            _path = path;
        }

        public override bool CanConvert(Type objectType)
        {
            return (Nullable.GetUnderlyingType(objectType) ?? objectType) == typeof(decimal);
        }

        public override void WriteJson(JsonWriter writer, object? value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteValue(
                FixedPointScale.ToScaled(
                    (decimal)value,
                    _scale,
                    _path));
        }

        public override object? ReadJson(
            JsonReader reader,
            Type objectType,
            object? existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            return FixedPointScale.FromScaled(
                ReadScaledValue(reader),
                _scale);
        }

        private static bool IsExactWholeNumber(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                Math.Abs(value) <= FixedPointScale.MaxExactMagnitude &&
                Math.Floor(value) == value;
        }

        /// <summary>
        ///     Reads the stored value as the integer it was written as. Cosmos DB hands a whole
        ///     number back as a JSON integer, but a value that passed through the double number
        ///     type of the database can come back as a float token - accepted as long as it is
        ///     still a whole number inside the exact range, and refused otherwise rather than
        ///     rounded into place.
        /// </summary>
        private long ReadScaledValue(JsonReader reader)
        {
            var value = reader.Value;

            switch (value)
            {
                case long longValue:
                    return longValue;
                case int intValue:
                    return intValue;
                case double doubleValue when IsExactWholeNumber(doubleValue):
                    return (long)doubleValue;
                case decimal decimalValue
                    when decimalValue == decimal.Truncate(decimalValue) &&
                        Math.Abs(decimalValue) <= FixedPointScale.MaxExactMagnitude:
                    return (long)decimalValue;
                default:
                    var description = Convert.ToString(
                        value,
                        CultureInfo.InvariantCulture) ?? "null";

                    throw FixedPointError.StoredValueIsNotScaled(
                        _path,
                        description);
            }
        }
    }
}
