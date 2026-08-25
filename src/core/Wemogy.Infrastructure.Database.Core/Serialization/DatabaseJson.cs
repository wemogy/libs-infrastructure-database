using System.Text.Json;
using System.Text.Json.Serialization;
using Wemogy.Infrastructure.Database.Core.ValueObjects;

namespace Wemogy.Infrastructure.Database.Core.Serialization;

/// <summary>
///     The System.Text.Json options the providers read caller supplied JSON with.
/// </summary>
public static class DatabaseJson
{
    /// <summary>
    ///     Reads the JSON a caller puts into <see cref="QueryFilter.Value"/> and
    ///     <see cref="QuerySorting.SearchAfter"/>.
    /// </summary>
    /// <remarks>
    ///     Both are JSON the caller writes rather than something this library produced, so the
    ///     reader stays as forgiving as the Newtonsoft.Json based one it replaces: an enum is
    ///     accepted under its name as well as its number, a number is accepted inside a string,
    ///     and a date is accepted in the spellings System.Text.Json rejects on its own. Without
    ///     that, a filter that has always worked would start to throw - and query building has no
    ///     try/catch around it, so it would throw at the call site rather than filter nothing.
    /// </remarks>
    public static readonly JsonSerializerOptions QueryValueOptions = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        Converters =
        {
            new JsonStringEnumConverter(),
            new LenientDateTimeOffsetJsonConverter(),
            new LenientDateTimeJsonConverter()
        }
    };
}
