using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Wemogy.Infrastructure.Database.Core.Attributes;
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
    internal class FixedPointDecimalConverter : JsonConverter<decimal>
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

        public override decimal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return FixedPointScaledJson.Read(
                ref reader,
                _scale,
                _path);
        }

        public override void Write(Utf8JsonWriter writer, decimal value, JsonSerializerOptions options)
        {
            FixedPointScaledJson.Write(
                writer,
                value,
                _scale,
                _path);
        }
    }
}
