using System.IO;
using System.Text;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Cosmos.Serialization;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Serialization;

/// <summary>
///     Covers how a member marked with <c>[FixedPoint]</c> reaches the document and comes back.
///     Read and write have to scale symmetrically, otherwise the value a caller stores is not the
///     value the next read hands out.
/// </summary>
public class FixedPointSerializationTests
{
    private readonly CosmosEntitySerializer _serializer = new CosmosEntitySerializer();

    [Fact]
    public void ToStream_ShouldWriteTheScaledInteger()
    {
        // Arrange
        var target = new PatchTarget
        {
            Balance = 0.5m,
            Discount = 12.34m,
            Inner = new PatchTargetInner { Amount = 1.2345m }
        };

        // Act
        var json = Serialize(target);

        // Assert: the integer is what makes the server-side increment of it exact
        json.ShouldContain("\"balance\":500000");
        json.ShouldContain("\"discount\":1234");
        json.ShouldContain("\"amount\":12345");
    }

    [Fact]
    public void ToStream_ShouldLeaveADecimalWithoutTheAttributeAlone()
    {
        // Act
        var json = Serialize(new PatchTarget { Money = 9.99m });

        // Assert
        json.ShouldContain("\"money\":9.99");
    }

    [Fact]
    public void FromStream_ShouldUndoTheScaling()
    {
        // Act
        var target = Deserialize("{\"balance\":500000,\"discount\":1234,\"inner\":{\"amount\":12345}}");

        // Assert
        target.Balance.ShouldBe(0.5m);
        target.Discount.ShouldBe(12.34m);
        target.Inner.Amount.ShouldBe(1.2345m);
    }

    [Fact]
    public void FromStream_ShouldAcceptAWholeNumberThatCameBackAsAFloat()
    {
        // Act: the database holds every number as a double, so a whole number can come back with
        // a decimal point on it
        var target = Deserialize("{\"balance\":500000.0}");

        // Assert
        target.Balance.ShouldBe(0.5m);
    }

    [Fact]
    public void FromStream_ShouldKeepANullNullable()
    {
        // Act
        var target = Deserialize("{\"balance\":0}");

        // Assert
        target.Discount.ShouldBeNull();
    }

    [Fact]
    public void FromStream_ShouldRefuseAStoredValueThatIsNotScaled()
    {
        // Act & Assert: a document written before the member was marked with the attribute is
        // reported instead of read as a value 10^6 times too small
        var exception = Should.Throw<UnexpectedErrorException>(() => Deserialize("{\"balance\":0.5}"));
        exception.Code.ShouldBe("FixedPointStoredValueIsNotScaled");
    }

    [Fact]
    public void FromStream_ShouldRefuseAStoredCounterThatGrewOutOfTheExactRange()
    {
        // Act & Assert: an accumulated increment can cross the bound without any single operand
        // doing so, and past it the stored number is no longer what the increments added up to.
        // Reported on both token kinds a whole number can arrive as
        var fromInteger = Should.Throw<UnexpectedErrorException>(() => Deserialize("{\"balance\":9007199254740993}"));
        var fromFloat = Should.Throw<UnexpectedErrorException>(() => Deserialize("{\"balance\":1e17}"));

        fromInteger.Code.ShouldBe("FixedPointStoredValueOutOfRange");
        fromFloat.Code.ShouldBe("FixedPointStoredValueOutOfRange");
    }

    [Fact]
    public void FromStream_ShouldAcceptTheLargestExactStoredValue()
    {
        // Act
        var target = Deserialize("{\"balance\":9007199254740991}");

        // Assert
        target.Balance.ShouldBe(9007199254.740991m);
    }

    [Fact]
    public void RoundTrip_ShouldPreserveAValueNoBinaryFloatingPointNumberCanHold()
    {
        // Arrange
        var target = new PatchTarget { Balance = 0.1m };

        // Act
        var roundTripped = Deserialize(Serialize(target));

        // Assert
        roundTripped.Balance.ShouldBe(0.1m);
    }

    [Fact]
    public void ToStream_ShouldRefuseAValueFinerThanTheDeclaredScale()
    {
        // Act & Assert
        var exception = Should.Throw<UnexpectedErrorException>(
            () => Serialize(new PatchTarget { Balance = 0.5000001m }));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    private string Serialize(PatchTarget target)
    {
        using var stream = _serializer.ToStream(target);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private PatchTarget Deserialize(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return _serializer.FromStream<PatchTarget>(stream);
    }
}
