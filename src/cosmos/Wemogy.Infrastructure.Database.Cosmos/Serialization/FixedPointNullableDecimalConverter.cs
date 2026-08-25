using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     <see cref="FixedPointDecimalConverter"/> for a nullable member.
    /// </summary>
    /// <remarks>
    ///     A converter assigned to a property rather than registered on the options has to match
    ///     the type of that property exactly, so System.Text.Json does not wrap the non-nullable
    ///     converter for a <c>decimal?</c> the way it would for one of its own.
    /// </remarks>
    internal class FixedPointNullableDecimalConverter : JsonConverter<decimal?>
    {
        private readonly int _scale;
        private readonly string _path;

        /// <summary>
        ///     Initializes a new instance of the <see cref="FixedPointNullableDecimalConverter"/>
        ///     class.
        /// </summary>
        /// <param name="scale">The declared scale of the member</param>
        /// <param name="path">The member the converter belongs to, for the error messages</param>
        public FixedPointNullableDecimalConverter(int scale, string path)
        {
            _scale = scale;
            _path = path;
        }

        public override decimal? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
            {
                return null;
            }

            return FixedPointScaledJson.Read(
                ref reader,
                _scale,
                _path);
        }

        public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            FixedPointScaledJson.Write(
                writer,
                value.Value,
                _scale,
                _path);
        }
    }
}
