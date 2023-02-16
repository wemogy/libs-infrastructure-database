using System;
using FluentAssertions;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Extensions;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;

public class TypeExtensionsTests
{
    [Theory]
    [InlineData(
        typeof(NotSoftDeletableClass),
        false)]
    [InlineData(
        typeof(SoftDeletableClass),
        true)]
    [InlineData(
        typeof(SoftDeletableClassSubClass),
        true)]
    public void ThrowIfNotSoftDeletableShouldWork(Type type, bool expectedToBeSoftDeletable)
    {
        // Act
        var exception = Record.Exception(() => type.ThrowIfNotSoftDeletable());

        // Assert
        if (expectedToBeSoftDeletable)
        {
            exception.Should().BeNull();
        }
        else
        {
            exception.Should().NotBeNull();
            exception.Should().BeOfType<UnexpectedErrorException>();
        }
    }

    private class NotSoftDeletableClass
    {
    }

    private class SoftDeletableClass
    {
        [SoftDeleteFlag]
        public bool Flag { get; set; }
    }

    private class SoftDeletableClassSubClass : SoftDeletableClass
    {
    }
}
