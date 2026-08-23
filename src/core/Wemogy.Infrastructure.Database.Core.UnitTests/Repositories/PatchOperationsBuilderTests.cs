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
