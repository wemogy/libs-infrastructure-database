using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     Writes a zero offset as the <c>Z</c> suffix instead of <c>+00:00</c>.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Cosmos DB compares and orders a timestamp as the string it is stored as, so an
    ///         ISO-8601 column only sorts correctly while every document spells the same instant
    ///         the same way. <c>+</c> is 0x2B and <c>Z</c> is 0x5A, so the two spellings interleave
    ///         wrongly under the ordinal comparison a range filter and the search-after cursor both
    ///         rely on.
    ///     </para>
    ///     <para>
    ///         The library-owned <c>CreatedAt</c>/<c>UpdatedAt</c> were written as
    ///         <c>2026-08-25T10:00:00Z</c> while they were a UTC <see cref="DateTime"/>. Keeping
    ///         that spelling is what lets a container hold documents written before and after the
    ///         move to <see cref="DateTimeOffset"/> and still order by either field.
    ///     </para>
    ///     <para>
    ///         An offset other than zero is left exactly as System.Text.Json would write it: it
    ///         carries information the consumer chose to store, and normalizing it away would be a
    ///         silent loss rather than a compatibility fix.
    ///     </para>
    /// </remarks>
    public class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
    {
        public override DateTimeOffset Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            // both spellings parse into the same instant, so a container written by either
            // version of the library reads back correctly
            return reader.GetDateTimeOffset();
        }

        public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
        {
            if (value.Offset == TimeSpan.Zero)
            {
                // a DateTime of Kind.Utc is what the writer spells with the "Z" suffix
                writer.WriteStringValue(value.UtcDateTime);
                return;
            }

            writer.WriteStringValue(value);
        }
    }
}
