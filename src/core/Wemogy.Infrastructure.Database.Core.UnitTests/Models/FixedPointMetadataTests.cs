using System.Collections.Generic;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Models;

/// <summary>
///     Covers how the <see cref="FixedPointAttribute"/> is read off an entity type and how the
///     values of an entity are validated against it - the check both providers run before they
///     write.
/// </summary>
public class FixedPointMetadataTests
{
    [Fact]
    public void GetScale_ShouldReturnNullForAMemberWithoutTheAttribute()
    {
        // Act & Assert
        FixedPointMetadata.GetScale(typeof(PatchTarget).GetProperty(nameof(PatchTarget.Money))!).ShouldBeNull();
    }

    [Fact]
    public void GetScale_ShouldReturnTheDeclaredScale()
    {
        // Act & Assert
        FixedPointMetadata.GetScale(typeof(PatchTarget).GetProperty(nameof(PatchTarget.Balance))!).ShouldBe(6);
        FixedPointMetadata.GetScale(typeof(PatchTarget).GetProperty(nameof(PatchTarget.Discount))!).ShouldBe(2);
    }

    [Fact]
    public void GetScale_ShouldRefuseTheAttributeOnANonDecimalMember()
    {
        // Act & Assert: only a decimal has a base-10 representation worth preserving
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointMetadata.GetScale(typeof(InvalidTarget).GetProperty(nameof(InvalidTarget.Count))!));
        exception.Code.ShouldBe("FixedPointMemberIsNotADecimal");
    }

    [Fact]
    public void GetScale_ShouldRefuseAScaleNoFactorCanBeBuiltFor()
    {
        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointMetadata.GetScale(typeof(InvalidTarget).GetProperty(nameof(InvalidTarget.TooPrecise))!));
        exception.Code.ShouldBe("FixedPointScaleOutOfRange");
    }

    [Fact]
    public void HasFixedPointMembers_ShouldFindAMemberBehindANestedPath()
    {
        // Act & Assert: PatchTargetInner carries the attribute, PatchTarget only reaches it
        FixedPointMetadata.HasFixedPointMembers(typeof(PatchTargetInner)).ShouldBeTrue();
        FixedPointMetadata.HasFixedPointMembers(typeof(PatchTarget)).ShouldBeTrue();
        FixedPointMetadata.HasFixedPointMembers(typeof(DataCenter)).ShouldBeFalse();
    }

    [Fact]
    public void GetScalesByPath_ShouldReportEveryReachableMemberAndIgnoreCase()
    {
        // Act
        var scales = FixedPointMetadata.GetScalesByPath(typeof(PatchTarget));

        // Assert: keyed by member path, looked up by the camelCased name a query uses
        scales["Balance"].ShouldBe(6);
        scales["Inner.Amount"].ShouldBe(4);
        scales["inner.amount"].ShouldBe(4);
        scales.ContainsKey(nameof(PatchTarget.Money)).ShouldBeFalse();
    }

    [Fact]
    public void EnsureValuesAreValid_ShouldAcceptAValueTheScaleCanStore()
    {
        // Act & Assert
        Should.NotThrow(
            () => FixedPointMetadata.EnsureValuesAreValid(
                new PatchTarget
                {
                    Balance = 0.5m,
                    Discount = 12.34m,
                    Inner = new PatchTargetInner { Amount = 1.2345m }
                }));
    }

    [Fact]
    public void EnsureValuesAreValid_ShouldRefuseAValueFinerThanTheScale()
    {
        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointMetadata.EnsureValuesAreValid(new PatchTarget { Balance = 0.5000001m }));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public void EnsureValuesAreValid_ShouldWalkIntoANestedMember()
    {
        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointMetadata.EnsureValuesAreValid(
                new PatchTarget { Inner = new PatchTargetInner { Amount = 1.23456m } }));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
        exception.Message.ShouldContain("Inner.Amount");
    }

    [Fact]
    public void EnsureValuesAreValid_ShouldWalkIntoACollection()
    {
        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointMetadata.EnsureValuesAreValid(
                new CollectionTarget
                {
                    Items = new List<PatchTargetInner> { new PatchTargetInner { Amount = 1.23456m } }
                }));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public void EnsureValuesAreValid_ShouldTerminateOnAGraphThatPointsBackAtItself()
    {
        // Arrange: a cycle would recurse forever without the reference check
        var target = new SelfReferencingTarget { Amount = 0.5m };
        target.Next = target;

        // Act & Assert
        Should.NotThrow(() => FixedPointMetadata.EnsureValuesAreValid(target));
    }

    private class InvalidTarget
    {
        [FixedPoint(Scale = 2)]
        public int Count { get; set; }

        [FixedPoint(Scale = 19)]
        public decimal TooPrecise { get; set; }
    }

    private class CollectionTarget
    {
        public List<PatchTargetInner> Items { get; set; } = new List<PatchTargetInner>();
    }

    private class SelfReferencingTarget
    {
        [FixedPoint(Scale = 6)]
        public decimal Amount { get; set; }

        public SelfReferencingTarget? Next { get; set; }
    }
}
