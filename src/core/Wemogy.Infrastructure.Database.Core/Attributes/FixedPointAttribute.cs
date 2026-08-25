using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

/// <summary>
///     Marks a <see cref="decimal"/> member as a fixed-point value: it is persisted as the
///     integer <c>value * 10^Scale</c> instead of as a floating point number, and read back by
///     dividing by the same factor.
///     <para>
///         This is what makes an exact increment of a decimal possible at all. The Cosmos number
///         type is IEEE 754 binary64 and its <c>Increment</c> takes a <c>long</c> or a
///         <c>double</c> - so a base-10 domain like money can only be incremented exactly by
///         moving it into whole units of <c>10^-Scale</c> first.
///     </para>
///     <para>
///         Exactness holds while the <em>scaled</em> value stays inside
///         ±<see cref="Models.FixedPointScale.MaxExactMagnitude"/>, which at scale 6 is roughly
///         ±9.0 × 10⁹ in domain units. A value written or incremented past that bound is refused
///         rather than silently degraded, but the database cannot check the accumulated result of
///         a server-side increment - keep the range of the counter inside the bound.
///     </para>
///     <para>
///         A value that carries more decimal places than the declared scale is refused on every
///         write path, so a stored value is always exactly the scaled integer divided by
///         <c>10^Scale</c> and both providers agree on what is stored.
///     </para>
///     <para>
///         Adding this attribute to a member of an already populated container changes how the
///         member is read, so it needs a migration of the existing documents.
///     </para>
/// </summary>
/// <example>
///     <code>
///     public class QuotaBalance : GlobalEntityBase
///     {
///         [FixedPoint(Scale = 6)]
///         public decimal Value { get; set; }
///     }
///     </code>
/// </example>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class FixedPointAttribute : Attribute
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="FixedPointAttribute"/> class.
    /// </summary>
    public FixedPointAttribute()
    {
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="FixedPointAttribute"/> class.
    /// </summary>
    /// <param name="scale">The number of decimal places the member is stored with</param>
    public FixedPointAttribute(int scale)
    {
        Scale = scale;
    }

    /// <summary>
    ///     The number of decimal places the member is stored with, between 0 and
    ///     <see cref="Models.FixedPointScale.MaxScale"/>. A scale of 6 stores <c>0.5</c> as
    ///     <c>500000</c>.
    /// </summary>
    public int Scale { get; set; }
}
