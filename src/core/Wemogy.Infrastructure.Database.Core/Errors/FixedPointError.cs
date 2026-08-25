using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Errors;

/// <summary>
///     The errors a member marked with the <see cref="FixedPointAttribute"/> can fail with. Both
///     providers throw through this class, so a value that one provider refuses is refused by the
///     other with the same exception type, error code and message.
/// </summary>
public static class FixedPointError
{
    /// <summary>
    ///     The attribute sits on a member that is not a <see cref="decimal"/>. Only a decimal has
    ///     a base-10 representation to preserve; every other numeric type is stored as it is.
    /// </summary>
    public static UnexpectedErrorException NotADecimal(MemberInfo member, string memberTypeName)
    {
        return Error.Unexpected(
            "FixedPointMemberIsNotADecimal",
            $"The member {member.DeclaringType?.Name}.{member.Name} is marked with [FixedPoint] but is a {memberTypeName}; only a decimal can be stored as a fixed-point value");
    }

    /// <summary>
    ///     The declared scale is outside the range a scaling factor can be built for.
    /// </summary>
    public static UnexpectedErrorException ScaleOutOfRange(MemberInfo member, int scale, int maxScale)
    {
        return Error.Unexpected(
            "FixedPointScaleOutOfRange",
            $"The member {member.DeclaringType?.Name}.{member.Name} declares a fixed-point scale of {scale}, which is outside the supported range of 0 to {maxScale}");
    }

    /// <summary>
    ///     The value carries more decimal places than the declared scale can store. Refused rather
    ///     than rounded: the caller decides how a value of its domain is rounded, and a silent
    ///     truncation of a money-like counter is exactly what the fixed-point encoding exists to
    ///     prevent.
    /// </summary>
    public static UnexpectedErrorException PrecisionExceeded(string path, decimal value, int scale)
    {
        return Error.Unexpected(
            "FixedPointPrecisionExceeded",
            $"The value {value} for {path} carries more decimal places than the declared fixed-point scale of {scale}; round it to {scale} decimal places before writing it");
    }

    /// <summary>
    ///     The scaled value is outside the range the database can hold exactly. Cosmos DB stores a
    ///     number as IEEE 754 binary64, which is exact for integers up to
    ///     ±<see cref="Models.FixedPointScale.MaxExactMagnitude"/> only.
    /// </summary>
    public static UnexpectedErrorException ValueOutOfRange(string path, decimal value, int scale, long maxExactMagnitude)
    {
        return Error.Unexpected(
            "FixedPointValueOutOfRange",
            $"The value {value} for {path} does not fit into a fixed-point scale of {scale}: scaled by 10^{scale} it exceeds {maxExactMagnitude}, beyond which the database can no longer hold the value exactly");
    }

    /// <summary>
    ///     A stored value that is not the scaled integer the member is written as, e.g. a document
    ///     written before the member was marked with the <see cref="FixedPointAttribute"/>.
    /// </summary>
    public static UnexpectedErrorException StoredValueIsNotScaled(string path, string value)
    {
        return Error.Unexpected(
            "FixedPointStoredValueIsNotScaled",
            $"The stored value {value} for {path} is not the scaled integer a fixed-point member is written as; documents written before the member was marked with [FixedPoint] have to be migrated");
    }

    /// <summary>
    ///     A query filters a fixed-point member by a value that is not a number, so it cannot be
    ///     scaled to what the document carries.
    /// </summary>
    public static UnexpectedErrorException FilterValueNotSupported(string propertyPath, string value)
    {
        return Error.Unexpected(
            "FixedPointFilterValueNotSupported",
            $"The filter value {value} for the fixed-point member {propertyPath} is not a number, so it cannot be scaled to the value the document carries");
    }

    /// <summary>
    ///     A predicate uses a fixed-point member in a way that cannot be rewritten to compare
    ///     against the scaled value the document carries. The Cosmos provider has to rewrite such
    ///     a predicate, because the field holds <c>500000</c> where the entity reads <c>0.5</c>.
    /// </summary>
    public static UnexpectedErrorException ExpressionNotSupported(string expression, string reason)
    {
        return Error.Unexpected(
            "FixedPointExpressionNotSupported",
            $"The expression {expression} cannot be translated for a fixed-point member: {reason}");
    }
}
