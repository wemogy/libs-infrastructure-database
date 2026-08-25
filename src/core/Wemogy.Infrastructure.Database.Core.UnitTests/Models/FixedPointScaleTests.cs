using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Models;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Models;

/// <summary>
///     Covers the arithmetic every provider scales through, so a value cannot be stored by one and
///     read differently by the other.
/// </summary>
public class FixedPointScaleTests
{
    [Theory]
    [InlineData("0.5", 6, 500000L)]
    [InlineData("-0.5", 6, -500000L)]
    [InlineData("0", 6, 0L)]
    [InlineData("100", 6, 100000000L)]
    [InlineData("0.000001", 6, 1L)]
    [InlineData("12.34", 2, 1234L)]
    [InlineData("7", 0, 7L)]
    public void ToScaled_ShouldScaleByThePowerOfTen(string value, int scale, long expected)
    {
        // Act
        var scaled = FixedPointScale.ToScaled(
            decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
            scale,
            "Entity.Value");

        // Assert
        scaled.ShouldBe(expected);
    }

    [Theory]
    [InlineData("0.5", 6)]
    [InlineData("0.1", 6)]
    [InlineData("-1234.5678", 4)]
    [InlineData("9007.199254", 6)]
    public void FromScaled_ShouldUndoToScaled(string value, int scale)
    {
        // Arrange
        var original = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var roundTripped = FixedPointScale.FromScaled(
            FixedPointScale.ToScaled(
                original,
                scale,
                "Entity.Value"),
            scale);

        // Assert
        roundTripped.ShouldBe(original);
    }

    [Fact]
    public void ToScaled_ShouldRefuseAValueFinerThanTheScale()
    {
        // Act & Assert: rounding a money-like counter behind the back of the caller is exactly
        // what the fixed-point encoding exists to prevent
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointScale.ToScaled(
                0.5000001m,
                6,
                "Entity.Value"));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public void ToScaled_ShouldAcceptTheLargestExactValue()
    {
        // Act: 2^53 - 1 scaled is the last integer the database still holds exactly
        var scaled = FixedPointScale.ToScaled(
            FixedPointScale.MaxExactMagnitude / 1000000m,
            6,
            "Entity.Value");

        // Assert
        scaled.ShouldBe(FixedPointScale.MaxExactMagnitude);
    }

    [Theory]
    [InlineData("9007199254.740992", 6)]
    [InlineData("-9007199254.740992", 6)]
    [InlineData("79228162514264337593543950335", 6)]
    public void ToScaled_ShouldRefuseAValueTheDatabaseCannotHoldExactly(string value, int scale)
    {
        // Act & Assert: past 2^53 - 1 the binary64 number type of the database starts skipping
        // integers, so the scaled value would be a double again
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointScale.ToScaled(
                decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture),
                scale,
                "Entity.Value"));
        exception.Code.ShouldBe("FixedPointValueOutOfRange");
    }
}
