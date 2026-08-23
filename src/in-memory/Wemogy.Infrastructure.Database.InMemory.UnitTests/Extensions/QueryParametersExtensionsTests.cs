using System.Collections.Generic;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Extensions;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Extensions;

/// <summary>
///     Covers the complex property syntax (<c>property&lt;ANY&gt;subProperty</c>) of the in-memory
///     expression builder. Unlike the Cosmos package, the in-memory client runs these expressions
///     for every query, so a filter that throws takes the whole query down.
/// </summary>
public class QueryParametersExtensionsTests
{
    [Fact]
    public void GetLambdaExpression_ShouldBuildAComplexAnyFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "versions<ANY>name",
                "\"v1\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(WithVersions("v0", "v1")).ShouldBeTrue();
        predicate(WithVersions("v0")).ShouldBeFalse();
        predicate(new QueryEntity { Versions = null! }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAComplexAnyFilterWithANonEqualsComparator()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "versions<ANY>name",
                "\"v1\"",
                Comparator.StartsWith));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(WithVersions("v1.2")).ShouldBeTrue();
        predicate(WithVersions("v2.0")).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAComplexAnyFilterOnANestedCollection()
    {
        // Arrange: the path to the collection is itself nested
        var queryParameters = ParametersFor(
            Filter(
                "address.versions<ANY>name",
                "\"v1\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(
                new QueryEntity
                {
                    Address = new QueryEntityAddress
                    {
                        Versions = new List<QueryEntityVersion> { new QueryEntityVersion { Name = "v1" } }
                    }
                })
            .ShouldBeTrue();
        predicate(new QueryEntity { Address = null }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAComplexAnyFilterOnANestedSubProperty()
    {
        // Arrange: the part behind the kind is a path, not a single member
        var queryParameters = ParametersFor(
            Filter(
                "versions<ANY>origin.name",
                "\"Germany\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(
                new QueryEntity
                {
                    Versions = new List<QueryEntityVersion>
                    {
                        new QueryEntityVersion { Origin = new QueryEntityCountry { Name = "Germany" } }
                    }
                })
            .ShouldBeTrue();
        predicate(
                new QueryEntity
                {
                    Versions = new List<QueryEntityVersion> { new QueryEntityVersion { Origin = null } }
                })
            .ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldThrowForAnUnknownComplexProperty()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "doesNotExist<ANY>name",
                "\"v1\"",
                Comparator.Equals));

        // Act
        var exception = Record.Exception(() => queryParameters.GetLambdaExpression<QueryEntity>());

        // Assert
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("DoesNotExist");
    }

    private static QueryEntity WithVersions(params string[] names)
    {
        var versions = new List<QueryEntityVersion>();
        foreach (var name in names)
        {
            versions.Add(new QueryEntityVersion { Name = name });
        }

        return new QueryEntity { Versions = versions };
    }

    private static QueryFilter Filter(string property, string value, Comparator comparator)
    {
        return new QueryFilter
        {
            Property = property,
            Value = value,
            Comparator = comparator
        };
    }

    private static QueryParameters ParametersFor(params QueryFilter[] filters)
    {
        return new QueryParameters { Filters = new List<QueryFilter>(filters) };
    }
}
