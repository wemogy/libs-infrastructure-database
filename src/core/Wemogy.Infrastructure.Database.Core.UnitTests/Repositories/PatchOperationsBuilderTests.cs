using System;
using System.Linq;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Core.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

/// <summary>
///     Covers the path shapes the repository tests cannot reach, because the entities they store
///     have neither a nested object nor a member narrower than the increment overloads.
/// </summary>
public class PatchOperationsBuilderTests
{
    [Fact]
    public void Build_ShouldResolveANestedPath()
    {
        // Act
        var operations = PatchOperationsBuilder<PatchTarget>.Build(p => p.Set(x => x.Inner.Value, 5L));

        // Assert: the members compose, outermost first, so a provider can build /inner/value
        operations.Single().Path.Select(x => x.Name).ShouldBe(new[] { "Inner", "Value" });
    }

    [Fact]
    public void Build_ShouldUnwrapTheNumericWideningOfANarrowerMember()
    {
        // Act: the compiler wraps an int member in a conversion to match Increment(..., long)
        var operations = PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => x.Counter, 3));

        // Assert
        var operation = operations.Single();
        operation.Kind.ShouldBe(DatabasePatchOperationKind.Increment);
        operation.Path.Single().Name.ShouldBe(nameof(PatchTarget.Counter));
        operation.Value.ShouldBe(3L);
    }

    [Fact]
    public void Build_ShouldRejectAComputedMember()
    {
        // Act & Assert: a computed member has no field in the document to patch
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => x.DoubledCounter, 1)));
        exception.Code.ShouldBe("PatchPathNotSupported");
    }

    [Fact]
    public void Build_ShouldRejectAPathThatIsNotRootedInTheParameter()
    {
        // Arrange
        var other = new PatchTarget { Name = "other" };

        // Act & Assert: a captured instance addresses no field of the document being patched
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Set(x => other.Name, "patched")));
        exception.Code.ShouldBe("PatchPathNotSupported");
    }

    [Fact]
    public void Build_ShouldRejectAFractionalIncrementOfAnIntegralMember()
    {
        // Act & Assert: it binds to the double overload, and the providers could only disagree
        // about the result of storing 0.5 in a field the entity reads back as an int
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => x.Counter, 0.5)));
        exception.Code.ShouldBe("PatchPathNotSupported");
    }

    [Fact]
    public void Build_ShouldRejectIncrementingADecimalMember()
    {
        // Act & Assert: the cast is the only way to reach an increment of a decimal, and money
        // must not travel through a double
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => (double)x.Money, 1.5)));
        exception.Code.ShouldBe("PatchPathNotSupported");
    }

    [Fact]
    public void Build_ShouldAllowSettingADecimalMember()
    {
        // Act: only incrementing a decimal is refused, writing one is not
        var operations = PatchOperationsBuilder<PatchTarget>.Build(p => p.Set(x => x.Money, 9.99m));

        // Assert
        operations.Single().Value.ShouldBe(9.99m);
    }

    [Fact]
    public void Build_ShouldScaleAnIncrementOfAFixedPointMember()
    {
        // Act: the increment travels as the integer the document carries, which is the only form
        // Cosmos DB can add exactly
        var operations = PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => x.Balance, 0.5m));

        // Assert
        var operation = operations.Single();
        operation.Kind.ShouldBe(DatabasePatchOperationKind.Increment);
        operation.Value.ShouldBe(500000L);
        operation.Scale.ShouldBe(6);
    }

    [Fact]
    public void Build_ShouldScaleASetOfAFixedPointMember()
    {
        // Act: a Set has to write the same encoding an increment adds to
        var operations = PatchOperationsBuilder<PatchTarget>.Build(p => p.Set(x => x.Inner.Amount, 1.2345m));

        // Assert
        var operation = operations.Single();
        operation.Value.ShouldBe(12345L);
        operation.Scale.ShouldBe(4);
    }

    [Fact]
    public void Build_ShouldScaleANullableFixedPointMemberAndKeepNull()
    {
        // Act
        var scaled = PatchOperationsBuilder<PatchTarget>.Build(p => p.Set(x => x.Discount, 12.34m));
        var cleared = PatchOperationsBuilder<PatchTarget>.Build(p => p.Set(x => x.Discount, null));

        // Assert: null is null at every scale
        scaled.Single().Value.ShouldBe(1234L);
        cleared.Single().Value.ShouldBeNull();
        cleared.Single().Scale.ShouldBe(2);
    }

    [Fact]
    public void Build_ShouldRejectAnIncrementFinerThanTheDeclaredScale()
    {
        // Act & Assert: a silent truncation of a money-like counter is what the encoding exists
        // to prevent
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => x.Balance, 0.5000001m)));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public void Build_ShouldRejectAnIncrementBeyondTheExactRange()
    {
        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => x.Balance, 10_000_000_000m)));
        exception.Code.ShouldBe("FixedPointValueOutOfRange");
    }

    [Fact]
    public void Build_ShouldRejectADecimalIncrementOfAMemberWithoutTheAttribute()
    {
        // Act & Assert: without the fixed-point encoding there is nothing exact to add to
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => x.Money, 1.5m)));
        exception.Code.ShouldBe("PatchPathNotSupported");
        exception.Message.ShouldContain("[FixedPoint]");
    }

    [Fact]
    public void Build_ShouldRejectAWholeNumberIncrementOfAFixedPointMember()
    {
        // Act & Assert: the member holds value * 10^6, so adding 1 would move it by 0.000001
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => (long)x.Balance, 1)));
        exception.Code.ShouldBe("PatchPathNotSupported");
    }

    [Fact]
    public void Build_ShouldRejectSettingANestedObjectCarryingAnInvalidFixedPointValue()
    {
        // Act & Assert: a Set can write a whole object, and the members inside it are stored the
        // same way as the ones a path addresses directly
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(
                p => p.Set(
                    x => x.Inner,
                    new PatchTargetInner { Amount = 1.23456m })));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public void Build_ShouldRejectAWholeNumberIncrementOfAFloatingPointMember()
    {
        // Act & Assert: only a cast reaches this, and the providers would disagree about the
        // result - Cosmos adds the whole number to the fractional value, the in-memory applier
        // would do integer arithmetic on it
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(p => p.Increment(x => (long)x.Rate, 1)));
        exception.Code.ShouldBe("PatchPathNotSupported");
    }

    [Fact]
    public void Build_ShouldReturnASnapshotOfTheOperations()
    {
        // Arrange: the callback receives the builder, so a caller can hold on to it
        IPatchOperations<PatchTarget>? retainedBuilder = null;
        var operations = PatchOperationsBuilder<PatchTarget>.Build(
            p =>
            {
                retainedBuilder = p;
                p.Set(x => x.Name, "first");
            });

        // Act: adding after the fact must not reach what was already handed to a provider, which
        // would otherwise apply more operations than were validated
        retainedBuilder!.Set(x => x.Counter, 1);

        // Assert
        operations.Count.ShouldBe(1);
        retainedBuilder.OperationCount.ShouldBe(2);
    }

    [Theory]
    [InlineData(nameof(PatchTarget.Id))]
    [InlineData(nameof(PatchTarget.PartitionKey))]
    [InlineData(nameof(PatchTarget.ETag))]
    public void Build_ShouldRejectTheIdThePartitionKeyAndTheETag(string member)
    {
        // Arrange
        var operations = new Action<IPatchOperations<PatchTarget>>[]
        {
            p => p.Set(x => x.Id, "patched"),
            p => p.Set(x => x.PartitionKey, "patched"),
            p => p.Set(x => x.ETag, "patched")
        };
        var operation = member switch
        {
            nameof(PatchTarget.Id) => operations[0],
            nameof(PatchTarget.PartitionKey) => operations[1],
            _ => operations[2]
        };

        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => PatchOperationsBuilder<PatchTarget>.Build(operation));
        exception.Code.ShouldBe("PatchPathNotAllowed");
    }
}
