using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Wemogy.Infrastructure.Database.Core.Serialization;

/// <summary>
///     Reads a timestamp that was stored as a <see cref="DateTime"/>, and writes a zero offset as
///     the <c>Z</c> suffix instead of <c>+00:00</c>.
/// </summary>
/// <remarks>
///     <para>
///         <b>Reading.</b> A <see cref="DateTime"/> does not carry its <see cref="DateTime.Kind"/>
///         in its value, so one whose Kind was <see cref="DateTimeKind.Unspecified"/> was stored
///         without any offset at all - <c>2026-08-25T10:00:00</c>. System.Text.Json reads such a
///         value into the offset of the <i>reading machine</i>, which is not an error but a wrong
///         instant, and a different wrong instant in Berlin than in a UTC container. It is taken
///         as UTC here instead, which is what the library always meant it to be. A value that does
///         carry an offset - <c>Z</c> or <c>+02:00</c> - keeps it.
///     </para>
///     <para>
///         A number is read as milliseconds since the unix epoch, which is the shape
///         <c>Wemogy.Core</c> writes a <see cref="DateTime"/> in.
///     </para>
///     <para>
///         <b>Writing.</b> Cosmos DB compares and orders a timestamp as the string it is stored
///         as, and an ISO-8601 column only sorts correctly while every document spells the same
///         instant the same way: <c>+</c> is 0x2B and <c>Z</c> is 0x5A, so the two forms interleave
///         wrongly under the ordinal comparison a range filter and a search-after cursor rely on.
///         The <c>Z</c> form is what was written while the field was a UTC
///         <see cref="DateTime"/>, so keeping it is what lets one container hold documents from
///         before and after the move to <see cref="DateTimeOffset"/> and still order by the field.
///     </para>
///     <para>
///         An offset other than zero is written exactly as System.Text.Json would: it carries
///         information the consumer chose to store, and normalizing it away would be a silent loss
///         rather than a compatibility fix.
///     </para>
/// </remarks>
public class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64());
        }

        var text = reader.GetString();
        if (text == null)
        {
            throw new JsonException("Cannot read a timestamp from a null value.");
        }

        // AssumeUniversal applies only when the text carries no offset of its own, so a stored
        // "Z" or "+02:00" is preserved while a bare DateTime stops picking up the reading
        // machine's zone
        if (!DateTimeOffset.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var value))
        {
            throw new JsonException($"Cannot read a timestamp from \"{text}\".");
        }

        return value;
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
