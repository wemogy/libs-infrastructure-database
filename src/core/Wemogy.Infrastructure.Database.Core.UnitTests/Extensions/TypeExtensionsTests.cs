using System;
using FluentAssertions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Extensions;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;

public class TypeExtensionsTests
{
    class NotSoftDeletableClass
    {
    }

    class SoftDeletableClass
    {
        [SoftDeleteFlag]
        public bool Flag { get; set; }
    }

    class SoftDeletableClassSubClass : SoftDeletableClass
    {
    }

    [Theory]
    [InlineData(typeof(NotSoftDeletableClass), false)]
    [InlineData(typeof(SoftDeletableClass), true)]
    [InlineData(typeof(SoftDeletableClassSubClass), true)]
    public void IsSoftDeletableShouldWork(Type type, bool expectedToBeSoftDeletable)
    {
        // Act
        var isSoftDeletable = type.IsSoftDeletable();

        // Assert
        isSoftDeletable.Should().Be(expectedToBeSoftDeletable);
    }
}
