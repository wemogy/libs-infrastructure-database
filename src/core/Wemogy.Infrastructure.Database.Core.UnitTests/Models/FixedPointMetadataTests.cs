using System.Collections;
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
    public void GetScalesByPath_ShouldAlsoRegisterTheSerializedName()
    {
        // Arrange: a query addresses a renamed member by its stored name, so looking only for the
        // CLR name would compare an unscaled value against the scaled document
        var scales = FixedPointMetadata.GetScalesByPath(
            typeof(RenamedTarget),
            member => member.Name == nameof(RenamedTarget.Renamed) ? "bal" : member.Name);

        // Assert
        scales["bal"].ShouldBe(6);
        scales[nameof(RenamedTarget.Renamed)].ShouldBe(6);
    }

    [Fact]
    public void EnsureValuesAreValid_ShouldWalkIntoTheValuesOfADictionary()
    {
        // Act & Assert: the Cosmos serializer scales a member behind a dictionary value, so this
        // guard has to see it too - otherwise a test against the in-memory provider passes on a
        // value the Cosmos write refuses
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointMetadata.EnsureValuesAreValid(
                new DictionaryTarget
                {
                    Map = new Dictionary<string, PatchTargetInner>
                    {
                        { "a", new PatchTargetInner { Amount = 1.23456m } }
                    }
                }));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
        exception.Message.ShouldContain("Map[a]");
    }

    [Fact]
    public void HasFixedPointMembers_ShouldFindAMemberBehindADictionaryValue()
    {
        // Act & Assert
        FixedPointMetadata.HasFixedPointMembers(typeof(DictionaryTarget)).ShouldBeTrue();
    }

    [Fact]
    public void EnsureValuesAreValid_ShouldWalkIntoAValueBehindAnObjectMember()
    {
        // Act & Assert: Newtonsoft resolves the contract of what it actually finds, so the Cosmos
        // serializer scales this member - and refusing it only there would leave the in-memory
        // provider accepting a value the real write rejects
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointMetadata.EnsureValuesAreValid(
                new PolymorphicTarget
                {
                    Payload = new PatchTargetInner { Amount = 1.23456m }
                }));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public void EnsureValuesAreValid_ShouldWalkIntoANonGenericCollection()
    {
        // Act & Assert: an ArrayList reports no element type to prune by, so the items are
        // inspected by their runtime type instead
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointMetadata.EnsureValuesAreValid(
                new PolymorphicTarget
                {
                    Payload = 1m,
                    Items = new ArrayList { new PatchTargetInner { Amount = 1.23456m } }
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

    /// <summary>
    ///     Carries a fixed-point value only at runtime, behind an <c>object</c> member and inside a
    ///     non-generic collection. It needs a fixed-point member of its own, because that is what
    ///     keeps the walk off the write path of every entity that does not use the feature.
    /// </summary>
    private class PolymorphicTarget
    {
        [FixedPoint(Scale = 6)]
        public decimal Balance { get; set; }

        public object? Payload { get; set; }

        public ArrayList Items { get; set; } = new ArrayList();
    }

    private class RenamedTarget
    {
        [FixedPoint(Scale = 6)]
        public decimal Renamed { get; set; }
    }

    private class DictionaryTarget
    {
        public Dictionary<string, PatchTargetInner> Map { get; set; } =
            new Dictionary<string, PatchTargetInner>();
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
