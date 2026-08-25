using System;
using System.Text.Json;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     Reads and writes a <see cref="decimal"/> as the integer <c>value * 10^Scale</c>, shared
    ///     by the converter of the nullable and of the non-nullable member.
    /// </summary>
    internal static class FixedPointScaledJson
    {
        /// <summary>
        ///     The largest magnitude a double is converted from at all. Past it the value is not a
        ///     scaled integer of any scale, and the conversion to a long would overflow rather
        ///     than report anything.
        /// </summary>
        private const double MaxDoubleMagnitude = 9.2e18;

        public static void Write(Utf8JsonWriter writer, decimal value, int scale, string path)
        {
            writer.WriteNumberValue(
                FixedPointScale.ToScaled(
                    value,
                    scale,
                    path));
        }

        public static decimal Read(ref Utf8JsonReader reader, int scale, string path)
        {
            return FixedPointScale.FromScaled(
                ReadScaledValue(
                    ref reader,
                    path),
                scale);
        }

        /// <summary>
        ///     Reads the stored value as the integer it was written as. Cosmos DB hands a whole
        ///     number back as a JSON integer, but a value that passed through the double number
        ///     type of the database can come back as a fractional one - accepted as long as it is
        ///     still a whole number, and refused otherwise rather than rounded into place.
        /// </summary>
        private static long ReadScaledValue(ref Utf8JsonReader reader, string path)
        {
            if (reader.TokenType == JsonTokenType.Number)
            {
                if (reader.TryGetInt64(out var longValue))
                {
                    return EnsureIsInExactRange(
                        longValue,
                        path);
                }

                if (reader.TryGetDecimal(out var decimalValue) && decimalValue == decimal.Truncate(decimalValue))
                {
                    return EnsureIsInExactRange(
                        decimalValue,
                        path);
                }

                if (reader.TryGetDouble(out var doubleValue) && IsWholeNumber(doubleValue))
                {
                    return EnsureIsInExactRange(
                        (long)doubleValue,
                        path);
                }
            }

            throw FixedPointError.StoredValueIsNotScaled(
                path,
                Describe(ref reader));
        }

        private static string Describe(ref Utf8JsonReader reader)
        {
            // the reader is only walked here to report what was found, on a path that throws
            using var document = JsonDocument.ParseValue(ref reader);
            return document.RootElement.GetRawText();
        }

        private static bool IsWholeNumber(double value)
        {
            return !double.IsNaN(value) &&
                !double.IsInfinity(value) &&
                Math.Abs(value) <= MaxDoubleMagnitude &&
                Math.Floor(value) == value;
        }

        /// <summary>
        ///     Refuses a stored value the database can no longer hold exactly. A write is checked
        ///     against the same bound, but the accumulated result of a server-side increment is
        ///     nobody's to check up front - so the counter having crossed the bound is reported
        ///     here, on the read that would otherwise hand out a value that is only approximately
        ///     what the increments added up to.
        /// </summary>
        private static long EnsureIsInExactRange(decimal scaled, string path)
        {
            if (Math.Abs(scaled) > FixedPointScale.MaxExactMagnitude)
            {
                throw FixedPointError.StoredValueOutOfRange(
                    path,
                    scaled,
                    FixedPointScale.MaxExactMagnitude);
            }

            return (long)scaled;
        }
    }
}
