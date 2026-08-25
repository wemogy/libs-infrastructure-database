using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Cosmos.Query;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Query;

/// <summary>
///     Covers the rewrite that lets a predicate ask its question against the scaled integer the
///     document carries. The repository suite proves the answers; this one proves the shapes the
///     rewrite understands and the ones it refuses instead of answering wrongly.
/// </summary>
public class FixedPointPredicateRewriterTests
{
    [Fact]
    public void Rewrite_ShouldScaleTheConstantOfAComparison()
    {
        // Act
        var rewritten = Rewrite(x => x.Balance <= 100m);

        // Assert: 100 domain units are 100000000 in the document
        Evaluate(
            rewritten,
            new PatchTarget { Balance = 100000000m }).ShouldBeTrue();
        Evaluate(
            rewritten,
            new PatchTarget { Balance = 100000001m }).ShouldBeFalse();
    }

    [Fact]
    public void Rewrite_ShouldScaleACapturedValue()
    {
        // Arrange
        var cap = 0.5m;

        // Act
        var rewritten = Rewrite(x => x.Balance < cap);

        // Assert
        Evaluate(
            rewritten,
            new PatchTarget { Balance = 499999m }).ShouldBeTrue();
        Evaluate(
            rewritten,
            new PatchTarget { Balance = 500000m }).ShouldBeFalse();
    }

    [Fact]
    public void Rewrite_ShouldUseTheScaleOfTheAddressedMember()
    {
        // Act: Discount is declared at scale 2, Inner.Amount at scale 4
        var discount = Rewrite(x => x.Discount == 12.34m);
        var amount = Rewrite(x => x.Inner.Amount > 1m);

        // Assert
        Evaluate(
            discount,
            new PatchTarget { Discount = 1234m }).ShouldBeTrue();
        Evaluate(
            amount,
            new PatchTarget { Inner = new PatchTargetInner { Amount = 10001m } }).ShouldBeTrue();
        Evaluate(
            amount,
            new PatchTarget { Inner = new PatchTargetInner { Amount = 10000m } }).ShouldBeFalse();
    }

    [Fact]
    public void Rewrite_ShouldKeepNullAsNull()
    {
        // Act
        var rewritten = Rewrite(x => x.Discount == null);

        // Assert
        Evaluate(
            rewritten,
            new PatchTarget { Discount = null }).ShouldBeTrue();
        Evaluate(
            rewritten,
            new PatchTarget { Discount = 0m }).ShouldBeFalse();
    }

    [Fact]
    public void Rewrite_ShouldLeaveAPredicateWithoutAFixedPointMemberAlone()
    {
        // Arrange
        Expression<Func<PatchTarget, bool>> predicate = x => x.Name == "a" && x.Counter > 1;

        // Act & Assert: the very same instance, so nothing is recompiled for the entity types
        // that carry a fixed-point member somewhere else
        FixedPointPredicateRewriter.Rewrite(predicate).ShouldBeSameAs(predicate);
    }

    [Fact]
    public void Rewrite_ShouldScaleInsideACompositeCondition()
    {
        // Act
        var rewritten = Rewrite(x => x.Name == "a" && (x.Balance > 1m || x.Counter == 7));

        // Assert
        Evaluate(
            rewritten,
            new PatchTarget { Name = "a", Balance = 1000001m }).ShouldBeTrue();
        Evaluate(
            rewritten,
            new PatchTarget { Name = "a", Balance = 1000000m }).ShouldBeFalse();
        Evaluate(
            rewritten,
            new PatchTarget { Name = "a", Balance = 0m, Counter = 7 }).ShouldBeTrue();
    }

    [Fact]
    public void Rewrite_ShouldScaleBothOperandsOfAnAddition()
    {
        // Act: adding a domain value to a stored one only works once it is scaled too
        var rewritten = Rewrite(x => x.Balance + 1m > 2m);

        // Assert
        Evaluate(
            rewritten,
            new PatchTarget { Balance = 1000001m }).ShouldBeTrue();
        Evaluate(
            rewritten,
            new PatchTarget { Balance = 1000000m }).ShouldBeFalse();
    }

    [Fact]
    public void Rewrite_ShouldRefuseAValueFinerThanTheDeclaredScale()
    {
        // Act & Assert: the comparison could only be answered by rounding the bound
        var exception = Should.Throw<UnexpectedErrorException>(() => Rewrite(x => x.Balance <= 0.5000001m));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public void Rewrite_ShouldRefuseAComparisonOfTwoDifferentScales()
    {
        // Act & Assert: 10^6 units against 10^2 units compares two different things
        var exception = Should.Throw<UnexpectedErrorException>(() => Rewrite(x => x.Balance == x.Discount));
        exception.Code.ShouldBe("FixedPointExpressionNotSupported");
    }

    [Fact]
    public void Rewrite_ShouldRefuseAComparisonAgainstAnotherFieldOfTheDocument()
    {
        // Act & Assert: the other side is not a value the client can scale up front
        var exception = Should.Throw<UnexpectedErrorException>(() => Rewrite(x => x.Balance > x.Money));
        exception.Code.ShouldBe("FixedPointExpressionNotSupported");
    }

    [Fact]
    public void Rewrite_ShouldRefuseAConversionOutOfDecimal()
    {
        // Act & Assert: a double cannot hold the scaled integer exactly
        var exception = Should.Throw<UnexpectedErrorException>(() => Rewrite(x => (double)x.Balance > 1d));
        exception.Code.ShouldBe("FixedPointExpressionNotSupported");
    }

    [Fact]
    public void Rewrite_ShouldRefuseAnAccessItCannotScale()
    {
        // Arrange
        var balances = new List<decimal> { 0.5m, 1m };

        // Act & Assert: refused rather than handed to the database unscaled, which would answer
        // the wrong question without saying so
        var exception = Should.Throw<UnexpectedErrorException>(() => Rewrite(x => balances.Contains(x.Balance)));
        exception.Code.ShouldBe("FixedPointExpressionNotSupported");
    }

    [Fact]
    public void Rewrite_ShouldRefuseAnAccessInsideANestedLambda()
    {
        // Act & Assert: the rewrite cannot reach into the lambda of an Any, and handing the
        // predicate to the database unchanged would compare 1 against the stored 1000000
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointPredicateRewriter.Rewrite<Wallet>(x => x.Items.Any(i => i.Balance > 1m)));
        exception.Code.ShouldBe("FixedPointExpressionNotSupported");
    }

    [Fact]
    public void Rewrite_ShouldRefuseAnAccessBehindAnIndexer()
    {
        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => FixedPointPredicateRewriter.Rewrite<Wallet>(x => x.Items[0].Balance > 1m));
        exception.Code.ShouldBe("FixedPointExpressionNotSupported");
    }

    [Fact]
    public void Rewrite_ShouldStillScaleAgainstACapturedEntityOfTheSameType()
    {
        // Arrange: the captured entity carries the decimal in memory, not the scaled integer, so
        // it has to be scaled rather than refused as an unreachable access
        var other = new PatchTarget { Balance = 0.5m };

        // Act
        var rewritten = Rewrite(x => x.Balance <= other.Balance);

        // Assert
        Evaluate(
            rewritten,
            new PatchTarget { Balance = 500000m }).ShouldBeTrue();
        Evaluate(
            rewritten,
            new PatchTarget { Balance = 500001m }).ShouldBeFalse();
    }

    private static Expression<Func<PatchTarget, bool>> Rewrite(Expression<Func<PatchTarget, bool>> predicate)
    {
        return FixedPointPredicateRewriter.Rewrite(predicate)!;
    }

    /// <summary>
    ///     Runs the rewritten predicate against an entity whose members carry the scaled integers,
    ///     which is how the database sees the document.
    /// </summary>
    private static bool Evaluate(Expression<Func<PatchTarget, bool>> predicate, PatchTarget storedEntity)
    {
        return predicate.Compile()(storedEntity);
    }

    /// <summary>
    ///     An entity whose fixed-point member sits inside a collection, which no rewrite can
    ///     reach.
    /// </summary>
    private class Wallet
    {
        public List<WalletItem> Items { get; set; } = new List<WalletItem>();
    }

    private class WalletItem
    {
        [FixedPoint(Scale = 6)]
        public decimal Balance { get; set; }
    }
}
