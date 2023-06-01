using System;
using Bogus.DataSets;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;

public static class DateExtensions
{
    public static DateTime PastDate(this Date date)
    {
        var past = date.Past();

        // remove milliseconds
        return new DateTime(
            past.Year,
            past.Month,
            past.Day,
            past.Hour,
            past.Minute,
            past.Second,
            DateTimeKind.Utc);
    }
}
