using System;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Models;

/// <summary>
///     The arithmetic behind the <see cref="Attributes.FixedPointAttribute"/>: it turns a decimal
///     into the integer the document carries and back. Both providers scale through this class, so
///     a value cannot be stored by one and read differently by the other.
/// </summary>
public static class FixedPointScale
{
    /// <summary>
    ///     The largest scale a factor can be built for: <c>10^18</c> is the largest power of ten
    ///     that fits into a <see cref="long"/>.
    /// </summary>
    public const int MaxScale = 18;

    /// <summary>
    ///     The largest magnitude a scaled value may have. Cosmos DB stores a number as IEEE 754
    ///     binary64, which represents every integer up to 2^53 − 1 exactly and starts skipping
    ///     integers beyond it - so a scaled value past this bound would be a double again.
    /// </summary>
    public const long MaxExactMagnitude = (1L << 53) - 1;

    private static readonly decimal[] Factors = BuildFactors();

    /// <summary>
    ///     Returns the integer the document carries for the given value, e.g. <c>500000</c> for
    ///     <c>0.5</c> at scale 6.
    /// </summary>
    /// <param name="value">The value as the entity carries it</param>
    /// <param name="scale">The declared scale of the member</param>
    /// <param name="path">The member the value belongs to, for the error messages</param>
    /// <returns>The scaled value</returns>
    public static long ToScaled(decimal value, int scale, string path)
    {
        decimal scaled;

        try
        {
            scaled = value * Factors[scale];
        }
        catch (OverflowException)
        {
            // a decimal holds at most ~7.9 × 10^28, so a large value multiplied by a large factor
            // overflows before it can be compared against the bound below
            throw FixedPointError.ValueOutOfRange(
                path,
                value,
                scale,
                MaxExactMagnitude);
        }

        // a value with more decimal places than the scale would have to be rounded to fit, and
        // rounding a money-like counter behind the back of the caller is what the fixed-point
        // encoding exists to prevent
        if (scaled != decimal.Truncate(scaled))
        {
            throw FixedPointError.PrecisionExceeded(
                path,
                value,
                scale);
        }

        if (scaled > MaxExactMagnitude || scaled < -MaxExactMagnitude)
        {
            throw FixedPointError.ValueOutOfRange(
                path,
                value,
                scale,
                MaxExactMagnitude);
        }

        return (long)scaled;
    }

    /// <summary>
    ///     Returns the value the entity carries for the given stored integer, e.g. <c>0.5</c> for
    ///     <c>500000</c> at scale 6.
    /// </summary>
    /// <param name="scaled">The value as the document carries it</param>
    /// <param name="scale">The declared scale of the member</param>
    /// <returns>The value as the entity carries it</returns>
    public static decimal FromScaled(long scaled, int scale)
    {
        return scaled / Factors[scale];
    }

    private static decimal[] BuildFactors()
    {
        var factors = new decimal[MaxScale + 1];
        var factor = 1m;

        for (var scale = 0; scale <= MaxScale; scale++)
        {
            factors[scale] = factor;
            factor *= 10m;
        }

        return factors;
    }
}
