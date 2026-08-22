using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Cosmos.Helpers;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Helpers;

public class CustomExpressionsTests
{
    private static readonly ParameterExpression Parameter =
        Expression.Parameter(
            typeof(QueryEntity),
            "x");

    [Fact]
    public void FalseIfPropertyIsNull_ShouldEvaluateTheTargetIfThePropertyIsNotNull()
    {
        // Arrange
        var predicate = Compile(
            CustomExpressions.FalseIfPropertyIsNull(
                Property(nameof(QueryEntity.Lastname)),
                Expression.Constant(true)));

        // Act & Assert
        predicate(new QueryEntity { Lastname = "Doe" }).ShouldBeTrue();
    }

    [Fact]
    public void FalseIfPropertyIsNull_ShouldShortCircuitIfThePropertyIsNull()
    {
        // Arrange: an unguarded string.Contains call, so only the guard added by
        // FalseIfPropertyIsNull can keep this from throwing a NullReferenceException
        var containsMethod = typeof(string).GetMethod(
            nameof(string.Contains),
            new[] { typeof(string) })!;
        var unguardedTarget = Expression.Call(
            Property(nameof(QueryEntity.Lastname)),
            containsMethod,
            Expression.Constant("Doe"));
        var guarded = Compile(
            CustomExpressions.FalseIfPropertyIsNull(
                Property(nameof(QueryEntity.Lastname)),
                unguardedTarget));

        // Act & Assert
        var unguarded = Compile(unguardedTarget);
        Should.Throw<NullReferenceException>(() => unguarded(new QueryEntity { Lastname = null }));
        guarded(new QueryEntity { Lastname = null }).ShouldBeFalse();
        guarded(new QueryEntity { Lastname = "Doe" }).ShouldBeTrue();
    }

    [Theory]
    [InlineData("John", "OH", true)]
    [InlineData("John", "john", true)]
    [InlineData("John", "Jane", false)]
    public void ContainsIgnoreCaseExpression_ShouldIgnoreCasing(string firstname, string search, bool expected)
    {
        // Arrange
        var predicate = Compile(
            CustomExpressions.ContainsIgnoreCaseExpression(
                Property(nameof(QueryEntity.Firstname)),
                Expression.Constant(search)));

        // Act & Assert
        predicate(new QueryEntity { Firstname = firstname }).ShouldBe(expected);
    }

    [Theory]
    [InlineData("John", "oh", true)]
    [InlineData("John", "OH", false)]
    public void ContainsExpressionString_ShouldRespectCasing(string firstname, string search, bool expected)
    {
        // Arrange
        var predicate = Compile(
            CustomExpressions.ContainsExpressionString(
                Property(nameof(QueryEntity.Firstname)),
                Expression.Constant(search)));

        // Act & Assert
        predicate(new QueryEntity { Firstname = firstname }).ShouldBe(expected);
    }

    [Fact]
    public void ContainsExpressionStringList_ShouldMatchAnItemOfTheList()
    {
        // Arrange
        var predicate = Compile(
            CustomExpressions.ContainsExpressionStringList(
                Property(nameof(QueryEntity.Tags)),
                Expression.Constant("admin")));

        // Act & Assert
        predicate(new QueryEntity { Tags = new List<string> { "user", "admin" } }).ShouldBeTrue();
        predicate(new QueryEntity { Tags = new List<string> { "user" } }).ShouldBeFalse();
        predicate(new QueryEntity { Tags = null! }).ShouldBeFalse();
    }

    [Fact]
    public void ContainsExpressionGuidList_ShouldMatchAnItemOfTheList()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var predicate = Compile(
            CustomExpressions.ContainsExpressionGuidList(
                Property(nameof(QueryEntity.GroupIds)),
                Expression.Constant(groupId)));

        // Act & Assert
        predicate(new QueryEntity { GroupIds = new List<Guid> { groupId } }).ShouldBeTrue();
        predicate(new QueryEntity { GroupIds = new List<Guid> { Guid.NewGuid() } }).ShouldBeFalse();
        predicate(new QueryEntity { GroupIds = null! }).ShouldBeFalse();
    }

    [Theory]
    [InlineData("John", "Jo", true)]
    [InlineData("John", "jo", false)]
    [InlineData("John", "hn", false)]
    public void StartsWithExpression_ShouldRespectCasing(string firstname, string search, bool expected)
    {
        // Arrange
        var predicate = Compile(
            CustomExpressions.StartsWithExpression(
                Property(nameof(QueryEntity.Firstname)),
                Expression.Constant(search)));

        // Act & Assert
        predicate(new QueryEntity { Firstname = firstname }).ShouldBe(expected);
    }

    [Theory]
    [InlineData("John", "jo", true)]
    [InlineData("John", "JO", true)]
    [InlineData("John", "hn", false)]
    public void StartsWithIgnoreCaseExpression_ShouldIgnoreCasing(string firstname, string search, bool expected)
    {
        // Arrange
        var predicate = Compile(
            CustomExpressions.StartsWithIgnoreCaseExpression(
                Property(nameof(QueryEntity.Firstname)),
                Expression.Constant(search)));

        // Act & Assert
        predicate(new QueryEntity { Firstname = firstname }).ShouldBe(expected);
    }

    [Fact]
    public void IsOneOfExpression_ShouldCheckWhetherTheValueListContainsTheProperty()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var predicate = Compile(
            CustomExpressions.IsOneOfExpression(
                Property(nameof(QueryEntity.TenantId)),
                Expression.Constant(new List<Guid> { tenantId })));

        // Act & Assert
        predicate(new QueryEntity { TenantId = tenantId }).ShouldBeTrue();
        predicate(new QueryEntity { TenantId = Guid.NewGuid() }).ShouldBeFalse();
    }

    [Fact]
    public void GetContainsExpression_ShouldResolveTheBuilderForAStringListProperty()
    {
        // Arrange & Act
        var builder = CustomExpressions.GetContainsExpression<QueryEntity>(nameof(QueryEntity.Tags));
        var predicate = Compile(
            builder(
                Property(nameof(QueryEntity.Tags)),
                Expression.Constant("admin")));

        // Assert
        predicate(new QueryEntity { Tags = new List<string> { "admin" } }).ShouldBeTrue();
    }

    [Fact]
    public void GetContainsExpression_ShouldResolveTheBuilderForAGuidListProperty()
    {
        // Arrange
        var groupId = Guid.NewGuid();

        // Act
        var builder = CustomExpressions.GetContainsExpression<QueryEntity>(nameof(QueryEntity.GroupIds));
        var predicate = Compile(
            builder(
                Property(nameof(QueryEntity.GroupIds)),
                Expression.Constant(groupId)));

        // Assert
        predicate(new QueryEntity { GroupIds = new List<Guid> { groupId } }).ShouldBeTrue();
    }

    [Fact]
    public void GetContainsExpression_ShouldResolveTheBuilderForAStringProperty()
    {
        // Arrange & Act
        var builder = CustomExpressions.GetContainsExpression<QueryEntity>(nameof(QueryEntity.Firstname));
        var predicate = Compile(
            builder(
                Property(nameof(QueryEntity.Firstname)),
                Expression.Constant("oh")));

        // Assert
        predicate(new QueryEntity { Firstname = "John" }).ShouldBeTrue();
    }

    [Fact]
    public void GetContainsExpression_ShouldThrowForUnsupportedPropertyTypes()
    {
        // Arrange & Act
        var exception = Record.Exception(
            () => CustomExpressions.GetContainsExpression<QueryEntity>(nameof(QueryEntity.Age)));

        // Assert
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain(nameof(QueryEntity.Age));
    }

    private static MemberExpression Property(string propertyName)
    {
        return Expression.Property(
            Parameter,
            propertyName);
    }

    private static Func<QueryEntity, bool> Compile(Expression expression)
    {
        return Expression.Lambda<Func<QueryEntity, bool>>(
                expression,
                Parameter)
            .Compile();
    }
}
