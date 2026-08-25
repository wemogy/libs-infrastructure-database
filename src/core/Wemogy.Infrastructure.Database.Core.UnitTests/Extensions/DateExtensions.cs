using System;
using Bogus.DataSets;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;

public static class DateExtensions
{
    /// <summary>
    ///     A past instant in UTC, without the sub second part.
    /// </summary>
    /// <remarks>
    ///     Bogus hands out an instant in the zone of the running machine, and an implicit
    ///     conversion to <see cref="DateTimeOffset"/> would carry that zone into the entity - the
    ///     assertions would then hold or fail depending on where the suite runs. The offset is
    ///     therefore pinned here instead of being inherited.
    /// </remarks>
    public static DateTimeOffset PastDate(this Date date)
    {
        var past = date.Past().ToUniversalTime();

        return new DateTimeOffset(
            past.Year,
            past.Month,
            past.Day,
            past.Hour,
            past.Minute,
            past.Second,
            TimeSpan.Zero);
    }
}
