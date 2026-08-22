using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;
using Wemogy.Infrastructure.Database.Cosmos.Models;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Extensions;

/// <summary>
///     Covers the SQL text that <c>CosmosDatabaseClient&lt;TEntity&gt;</c> actually sends to
///     Cosmos: <c>GetFeedIterator</c> calls <c>GetItemQueryIterator</c>, which builds its
///     <see cref="QueryDefinition"/> through the private <c>GetQueryDefinition</c>. The expression
///     builders covered by <see cref="QueryParametersExtensionsTests"/> are a separate, LINQ-based
///     code path that the Cosmos client does not use.
///     <para>
///         The builder is private and takes a <see cref="Container"/>, so it is invoked through
///         reflection against an offline <see cref="CosmosClient"/>. Neither creating the client nor
///         building the query definition performs any I/O, so these tests need no emulator.
///     </para>
/// </summary>
public class CosmosQueryDefinitionTests
{
    private const string OfflineConnectionString =
        "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    private static readonly MethodInfo GetQueryDefinitionMethod =
        typeof(QueryParametersExtensions).GetMethod(
            "GetQueryDefinition",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            "QueryParametersExtensions.GetQueryDefinition was not found. If it was renamed, update this test.");

    [Fact]
    public void GetQueryDefinition_ShouldOnlySelectAndFromWithoutFiltersAndSortings()
    {
        // Arrange
        var queryParameters = new QueryParameters();

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert
        queryText.ShouldBe("SELECT VALUE c FROM users c");
    }

    [Fact]
    public void GetQueryDefinition_ShouldCamelCaseThePropertyNamesOfFilters()
    {
        // Arrange: callers may pass PascalCase properties, Cosmos documents are camelCased
        var queryParameters = ParametersFor(
            Filter(
                "Firstname",
                "\"John\"",
                Comparator.Equals));

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert
        queryText.ShouldContain("c.firstname =");
        queryText.ShouldNotContain(
            "c.Firstname",
            Case.Sensitive);
    }

    [Fact]
    public void GetQueryDefinition_ShouldParameterizeAnEqualsFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals));

        // Act
        var queryDefinition = Build(queryParameters);

        // Assert: the value must travel as a parameter, never inlined into the query text
        var parameter = queryDefinition.GetQueryParameters().Single();
        parameter.Value.ShouldBe("John");
        Normalize(queryDefinition.QueryText).ShouldBe($"SELECT VALUE c FROM users c WHERE c.firstname = {parameter.Name}");
    }

    [Fact]
    public void GetQueryDefinition_ShouldUseIsDefinedAndIsNullForAnEqualsNullFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "lastname",
                "null",
                Comparator.Equals));

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert: "= null" never matches in Cosmos, missing and null have to be checked explicitly
        queryText.ShouldContain("IS_DEFINED(c.lastname) = false OR IS_NULL(c.lastname) = true");
    }

    [Fact]
    public void GetQueryDefinition_ShouldUseIsDefinedAndIsNullForANotEqualsNullFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "lastname",
                "null",
                Comparator.NotEquals));

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert
        queryText.ShouldContain("IS_DEFINED(c.lastname) AND IS_NULL(c.lastname) = false");
    }

    [Fact]
    public void GetQueryDefinition_ShouldParameterizeANotEqualsFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"John\"",
                Comparator.NotEquals));

        // Act
        var queryDefinition = Build(queryParameters);

        // Assert
        Normalize(queryDefinition.QueryText).ShouldContain("c.firstname != @param");
        queryDefinition.GetQueryParameters().Single().Value.ShouldBe("John");
    }

    [Fact]
    public void GetQueryDefinition_ShouldUseCaseSensitiveStartswithForStartsWith()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"Jo\"",
                Comparator.StartsWith));

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert: the third STARTSWITH argument is Cosmos' ignoreCase flag
        queryText.ShouldMatch(@"STARTSWITH\(c\.firstname, @param\w+, false\)");
    }

    [Fact]
    public void GetQueryDefinition_ShouldUseCaseInsensitiveStartswithForStartsWithIgnoreCase()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"jo\"",
                Comparator.StartsWithIgnoreCase));

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert
        queryText.ShouldMatch(@"STARTSWITH\(c\.firstname, @param\w+, true\)");
    }

    [Fact]
    public void GetQueryDefinition_ShouldUseArrayLengthForIsEmpty()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "tags",
                "\"\"",
                Comparator.IsEmpty));

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert
        queryText.ShouldContain("ARRAY_LENGTH(c.tags) = 0");
    }

    [Fact]
    public void GetQueryDefinition_ShouldUseArrayLengthForIsNotEmpty()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "tags",
                "\"\"",
                Comparator.IsNotEmpty));

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert
        queryText.ShouldContain("ARRAY_LENGTH(c.tags) > 0");
    }

    [Fact]
    public void GetQueryDefinition_ShouldUseArrayContainsForContains()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "tags",
                "\"admin\"",
                Comparator.Contains));

        // Act
        var queryDefinition = Build(queryParameters);

        // Assert
        var parameter = queryDefinition.GetQueryParameters().Single();
        Normalize(queryDefinition.QueryText).ShouldContain($"ARRAY_CONTAINS(c.tags, {parameter.Name})");
        parameter.Value.ShouldBe("admin");
    }

    [Fact]
    public void GetQueryDefinition_ShouldExpandIsOneOfIntoABracketedOrChain()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "[\"John\",\"Jane\"]",
                Comparator.IsOneOf));

        // Act
        var queryDefinition = Build(queryParameters);

        // Assert: one parameter per item, OR-ed together and bracketed so a following
        // AND filter cannot bind to the last OR branch only
        var parameters = queryDefinition.GetQueryParameters();
        parameters.Select(x => x.Value).ShouldBe(new object[] { "John", "Jane" });
        var queryText = Normalize(queryDefinition.QueryText);
        queryText.ShouldContain(" OR ");
        queryText.ShouldContain(
            $"WHERE ((c.firstname = {parameters[0].Name}) OR (c.firstname = {parameters[1].Name}))");
    }

    [Fact]
    public void GetQueryDefinition_ShouldCombineMultipleFiltersWithAnd()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals),
            Filter(
                "lastname",
                "\"Doe\"",
                Comparator.Equals));

        // Act
        var queryDefinition = Build(queryParameters);

        // Assert
        var parameters = queryDefinition.GetQueryParameters();
        parameters.Count.ShouldBe(2);
        Normalize(queryDefinition.QueryText)
            .ShouldContain($"WHERE c.firstname = {parameters[0].Name} AND c.lastname = {parameters[1].Name}");
    }

    [Theory]
    [InlineData(Comparator.EndsWith)]
    [InlineData(Comparator.NotContains)]
    [InlineData(Comparator.ContainsIgnoreCase)]
    [InlineData(Comparator.GreaterThan)]
    [InlineData(Comparator.Fuzzy)]
    public void GetQueryDefinition_ShouldSilentlyDropUnsupportedComparators(Comparator comparator)
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"John\"",
                comparator));

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert: the filter is logged and skipped, so the query returns MORE rows than asked for
        // rather than failing. Pinned here because it is a silent-widening behaviour.
        queryText.ShouldBe("SELECT VALUE c FROM users c");
    }

    [Fact]
    public void GetQueryDefinition_ShouldBuildACommaSeparatedOrderBy()
    {
        // Arrange
        var queryParameters = new QueryParameters
        {
            Sortings = new List<QuerySorting>
            {
                new QuerySorting
                {
                    OrderBy = "lastname",
                    SortOrder = SortOrder.Ascending
                },
                new QuerySorting
                {
                    OrderBy = "createdAt",
                    SortOrder = SortOrder.Descending
                }
            }
        };

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert
        queryText.ShouldContain("ORDER BY c.lastname ASC , c.createdAt DESC");
    }

    [Fact]
    public void GetQueryDefinition_ShouldTranslateTakeIntoOffsetAndLimit()
    {
        // Arrange
        var queryParameters = new QueryParameters { Take = 25 };

        // Act
        var queryText = QueryTextFor(queryParameters);

        // Assert
        queryText.ShouldEndWith("OFFSET 0 LIMIT 25");
    }

    [Fact]
    public void GetQueryDefinition_ShouldBuildTheSearchAfterTieBreakerChain()
    {
        // Arrange: keyset pagination over two columns needs
        // a > A OR (a = A AND b > B)
        var queryParameters = new QueryParameters
        {
            Sortings = new List<QuerySorting>
            {
                new QuerySorting
                {
                    OrderBy = "lastname",
                    SearchAfter = "\"Doe\""
                },
                new QuerySorting
                {
                    OrderBy = "age",
                    SearchAfter = "30"
                }
            }
        };

        // Act
        var queryDefinition = Build(queryParameters);

        // Assert
        var parameters = queryDefinition.GetQueryParameters();
        parameters.Select(x => x.Value).ShouldBe(new object[] { "Doe", 30L });
        Normalize(queryDefinition.QueryText).ShouldContain(
            $"WHERE ((c.lastname > {parameters[0].Name}) " +
            $"OR (c.lastname = {parameters[0].Name} AND c.age > {parameters[1].Name}))");
    }

    [Fact]
    public void GetQueryDefinition_ShouldNotAddAWhereClauseForAnUnfilteredGeneralFilter()
    {
        // Arrange: an untouched IQueryable stringifies to the container link, not to SQL
        var container = GetContainer();
        var generalFilter = container.GetItemLinqQueryable<QueryEntity>();

        // Act
        var queryText = Normalize(
            Build(
                new QueryParameters(),
                generalFilter,
                container).QueryText);

        // Assert
        queryText.ShouldBe("SELECT VALUE c FROM users c");
    }

    [Fact]
    public void GetQueryDefinition_ShouldKeepTheOwnFiltersForAnUnfilteredGeneralFilter()
    {
        // Arrange
        var container = GetContainer();
        var generalFilter = container.GetItemLinqQueryable<QueryEntity>();
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals));

        // Act
        var queryDefinition = Build(
            queryParameters,
            generalFilter,
            container);

        // Assert: nothing is spliced in, but the caller's own filters must survive
        Normalize(queryDefinition.QueryText).ShouldBe(
            "SELECT VALUE c FROM users c WHERE c.firstname = " +
            queryDefinition.GetQueryParameters().Single().Name);
    }

    [Fact]
    public void GetQueryDefinition_ShouldNotAddAJoinForAnUnfilteredGeneralFilter()
    {
        // Arrange
        var container = GetContainer();

        // Act
        var queryText = Normalize(
            Build(
                new QueryParameters { Take = 5 },
                container.GetItemLinqQueryable<QueryEntity>(),
                container).QueryText);

        // Assert
        queryText.ShouldBe("SELECT VALUE c FROM users c OFFSET 0 LIMIT 5");
    }

    [Fact]
    public void GetQueryDefinition_ShouldUseTheGeneralFilterAsWhereClauseWhenThereAreNoFilters()
    {
        // Arrange
        var container = GetContainer();
        var generalFilter = container.GetItemLinqQueryable<QueryEntity>().Where(x => x.Age > 18);

        // Act
        var queryText = Normalize(
            Build(
                new QueryParameters(),
                generalFilter,
                container).QueryText);

        // Assert: the root alias of the LINQ translation is rewritten to the container alias
        queryText.ShouldBe("SELECT VALUE c FROM users c WHERE (c[\"Age\"] > 18)");
    }

    [Fact]
    public void GetQueryDefinition_ShouldAndTheGeneralFilterInFrontOfTheOwnFilters()
    {
        // Arrange
        var container = GetContainer();
        var generalFilter = container.GetItemLinqQueryable<QueryEntity>().Where(x => x.Age > 18);
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals));

        // Act
        var queryDefinition = Build(
            queryParameters,
            generalFilter,
            container);

        // Assert: the read filter must never be OR-ed away by the caller's filters
        Normalize(queryDefinition.QueryText).ShouldContain(
            $"WHERE (c[\"Age\"] > 18) AND c.firstname = {queryDefinition.GetQueryParameters().Single().Name}");
    }

    [Fact]
    public void GetItemQueryIterator_ShouldBuildAnIteratorForTheQueryParameters()
    {
        // Arrange
        var container = GetContainer();

        // Act
        var feedIterator = container.GetItemQueryIterator<QueryEntity, string>(
            new QueryParameters(),
            new MappingMetadata(),
            container.GetItemLinqQueryable<QueryEntity>(),
            null);

        // Assert: building the iterator must not require a reachable account
        feedIterator.ShouldNotBeNull();
        feedIterator.HasMoreResults.ShouldBeTrue();
    }

    [Fact]
    public void GetCount_ShouldBuildAnIteratorForTheQueryParameters()
    {
        // Arrange
        var container = GetContainer();

        // Act
        var feedIterator = container.GetCount(
            new QueryParameters(),
            new MappingMetadata(),
            false,
            container.GetItemLinqQueryable<QueryEntity>(),
            null);

        // Assert
        feedIterator.ShouldNotBeNull();
        feedIterator.HasMoreResults.ShouldBeTrue();
    }

    private static Container GetContainer()
    {
        return new CosmosClient(OfflineConnectionString)
            .GetDatabase("testdb")
            .GetContainer("users");
    }

    private static QueryDefinition Build(
        QueryParameters queryParameters,
        IQueryable? generalFilter = null,
        Container? container = null)
    {
        return (QueryDefinition)GetQueryDefinitionMethod.Invoke(
            null,
            new object?[]
            {
                container ?? GetContainer(),
                "SELECT VALUE c",
                queryParameters,
                new MappingMetadata(),
                generalFilter,
                null
            })!;
    }

    private static string QueryTextFor(QueryParameters queryParameters)
    {
        return Normalize(Build(queryParameters).QueryText);
    }

    /// <summary>
    ///     The builder assembles the statement from an indented interpolated string and appends a
    ///     trailing space to every condition, so the assertions compare a collapsed form: runs of
    ///     whitespace become a single space and padding directly inside brackets is removed.
    /// </summary>
    private static string Normalize(string queryText)
    {
        var collapsed = Regex.Replace(
            queryText,
            @"\s+",
            " ");
        collapsed = Regex.Replace(
            collapsed,
            @"\(\s+",
            "(");
        collapsed = Regex.Replace(
            collapsed,
            @"\s+\)",
            ")");
        return collapsed.Trim();
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
