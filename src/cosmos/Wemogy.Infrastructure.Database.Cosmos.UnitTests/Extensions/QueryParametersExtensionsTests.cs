using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Extensions;

public class QueryParametersExtensionsTests
{
    [Fact]
    public void GetLambdaExpression_ShouldMatchEverythingWithoutFilters()
    {
        // Arrange
        var queryParameters = new QueryParameters();

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity()).ShouldBeTrue();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAnEqualsFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Firstname = "John" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "Jane" }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildANotEqualsFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"John\"",
                Comparator.NotEquals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Firstname = "Jane" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "John" }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAStartsWithFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"Jo\"",
                Comparator.StartsWith));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Firstname = "John" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "john" }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAStartsWithIgnoreCaseFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"jo\"",
                Comparator.StartsWithIgnoreCase));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Firstname = "John" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "Jane" }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAContainsIgnoreCaseFilter()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "\"OH\"",
                Comparator.ContainsIgnoreCase));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Firstname = "John" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "Jane" }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAContainsFilterForAStringList()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "tags",
                "\"admin\"",
                Comparator.Contains));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Tags = new List<string> { "user", "admin" } }).ShouldBeTrue();
        predicate(new QueryEntity { Tags = new List<string> { "user" } }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAContainsFilterForAGuidList()
    {
        // Arrange
        var groupId = Guid.NewGuid();
        var queryParameters = ParametersFor(
            Filter(
                "groupIds",
                $"\"{groupId}\"",
                Comparator.Contains));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { GroupIds = new List<Guid> { groupId } }).ShouldBeTrue();
        predicate(new QueryEntity { GroupIds = new List<Guid> { Guid.NewGuid() } }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAnIsOneOfFilter()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var queryParameters = ParametersFor(
            Filter(
                "tenantId",
                $"[\"{tenantA}\",\"{tenantB}\"]",
                Comparator.IsOneOf));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { TenantId = tenantA }).ShouldBeTrue();
        predicate(new QueryEntity { TenantId = tenantB }).ShouldBeTrue();
        predicate(new QueryEntity { TenantId = Guid.NewGuid() }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldThrowForIsOneOfOnANonGuidProperty()
    {
        // Arrange: CustomExpressions.IsOneOfExpression hardcodes List<Guid>.Contains(Guid),
        // so IsOneOf only works for Guid properties. Pinned so the passing Guid test above is
        // not read as "IsOneOf is generally supported".
        var queryParameters = ParametersFor(
            Filter(
                "firstname",
                "[\"John\"]",
                Comparator.IsOneOf));

        // Act
        var exception = Record.Exception(() => queryParameters.GetLambdaExpression<QueryEntity>());

        // Assert
        exception.ShouldBeOfType<ArgumentException>();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAFilterOnANestedProperty()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "address.city",
                "\"Berlin\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Address = new QueryEntityAddress { City = "Berlin" } }).ShouldBeTrue();
        predicate(new QueryEntity { Address = new QueryEntityAddress { City = "Munich" } }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldNotThrowIfANestedPropertyIsNull()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "address.city",
                "\"Berlin\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert: the generated null check guards the property access
        predicate(new QueryEntity { Address = null }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldNotThrowIfADeeplyNestedPropertyIsNull()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "address.country.name",
                "\"Germany\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Address = null }).ShouldBeFalse();
        predicate(new QueryEntity { Address = new QueryEntityAddress { Country = null } }).ShouldBeFalse();
        predicate(
                new QueryEntity
                {
                    Address = new QueryEntityAddress
                    {
                        Country = new QueryEntityCountry { Name = "Germany" }
                    }
                })
            .ShouldBeTrue();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAComplexAnyFilter()
    {
        // Arrange: "versions<ANY>name" means "any item of the Versions list matches"
        var queryParameters = ParametersFor(
            Filter(
                "versions<ANY>name",
                "\"v1\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(
                new QueryEntity
                {
                    Versions = new List<QueryEntityVersion>
                    {
                        new QueryEntityVersion { Name = "v0" },
                        new QueryEntityVersion { Name = "v1" }
                    }
                })
            .ShouldBeTrue();
        predicate(
                new QueryEntity
                {
                    Versions = new List<QueryEntityVersion> { new QueryEntityVersion { Name = "v0" } }
                })
            .ShouldBeFalse();
        predicate(new QueryEntity { Versions = null! }).ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAComplexAnyFilterWithANonEqualsComparator()
    {
        // Arrange: the comparator applies to the sub property of the collection item
        var queryParameters = ParametersFor(
            Filter(
                "versions<ANY>name",
                "\"v1\"",
                Comparator.StartsWith));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert
        predicate(
                new QueryEntity
                {
                    Versions = new List<QueryEntityVersion> { new QueryEntityVersion { Name = "v1.2" } }
                })
            .ShouldBeTrue();
        predicate(
                new QueryEntity
                {
                    Versions = new List<QueryEntityVersion> { new QueryEntityVersion { Name = "v2.0" } }
                })
            .ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldBuildAComplexAnyFilterOnANestedCollection()
    {
        // Arrange: the path to the collection is itself nested, which is the case that the
        // slash-based path resolver could never handle
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
        predicate(
                new QueryEntity
                {
                    Address = new QueryEntityAddress
                    {
                        Versions = new List<QueryEntityVersion> { new QueryEntityVersion { Name = "v0" } }
                    }
                })
            .ShouldBeFalse();
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
                    Versions = new List<QueryEntityVersion>
                    {
                        new QueryEntityVersion { Origin = new QueryEntityCountry { Name = "France" } }
                    }
                })
            .ShouldBeFalse();

        // a null sub property must not throw either
        predicate(
                new QueryEntity
                {
                    Versions = new List<QueryEntityVersion> { new QueryEntityVersion { Origin = null } }
                })
            .ShouldBeFalse();
    }

    [Fact]
    public void GetLambdaExpression_ShouldNotThrowIfTheParentOfANestedCollectionIsNull()
    {
        // Arrange
        var queryParameters = ParametersFor(
            Filter(
                "address.versions<ANY>name",
                "\"v1\"",
                Comparator.Equals));

        // Act
        var predicate = queryParameters.GetLambdaExpression<QueryEntity>().Compile();

        // Assert: the null check is added for the path to the collection, not only for the item
        predicate(new QueryEntity { Address = null }).ShouldBeFalse();
        predicate(new QueryEntity { Address = new QueryEntityAddress { Versions = null! } }).ShouldBeFalse();
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

        // Assert: an unresolvable path must name the property it could not find
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("DoesNotExist");
    }

    [Fact]
    public void BuildExpressionTree_ShouldCombineFiltersOfTheSameGroupWithAnd()
    {
        // Arrange: expression tree node id 0 => AND
        var filters = new List<QueryFilter>
        {
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals),
            Filter(
                "lastname",
                "\"Doe\"",
                Comparator.Equals)
        };

        // Act
        var predicate = QueryParametersExtensions.BuildExpressionTree<QueryEntity>(filters).Compile();

        // Assert
        predicate(
                new QueryEntity
                {
                    Firstname = "John",
                    Lastname = "Doe"
                })
            .ShouldBeTrue();
        predicate(
                new QueryEntity
                {
                    Firstname = "John",
                    Lastname = "Smith"
                })
            .ShouldBeFalse();
    }

    [Fact]
    public void BuildExpressionTree_ShouldCombineFiltersOfTheSameGroupWithOr()
    {
        // Arrange: expression tree node id 1 => OR
        var filters = new List<QueryFilter>
        {
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals,
                1),
            Filter(
                "firstname",
                "\"Jane\"",
                Comparator.Equals,
                1)
        };

        // Act
        var predicate = QueryParametersExtensions.BuildExpressionTree<QueryEntity>(filters).Compile();

        // Assert
        predicate(new QueryEntity { Firstname = "John" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "Jane" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "Joe" }).ShouldBeFalse();
    }

    [Fact]
    public void BuildExpressionTree_ShouldCombineMoreThanTwoFiltersOfTheSameGroup()
    {
        // Arrange
        var filters = new List<QueryFilter>
        {
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals,
                1),
            Filter(
                "firstname",
                "\"Jane\"",
                Comparator.Equals,
                1),
            Filter(
                "firstname",
                "\"Joe\"",
                Comparator.Equals,
                1)
        };

        // Act
        var predicate = QueryParametersExtensions.BuildExpressionTree<QueryEntity>(filters).Compile();

        // Assert
        predicate(new QueryEntity { Firstname = "Joe" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "Jack" }).ShouldBeFalse();
    }

    [Fact]
    public void BuildExpressionTree_ShouldNestSubGroupsIntoTheirParentGroup()
    {
        // Arrange: firstname = "John" AND (lastname = "Doe" OR lastname = "Smith")
        // level 0 / group 0 / AND holds the firstname filter,
        // level 1 / group 1 / OR is the child group of group 0
        var filters = new List<QueryFilter>
        {
            Filter(
                "firstname",
                "\"John\"",
                Comparator.Equals,
                0),
            Filter(
                "lastname",
                "\"Doe\"",
                Comparator.Equals,
                10011),
            Filter(
                "lastname",
                "\"Smith\"",
                Comparator.Equals,
                10011)
        };

        // Act
        var predicate = QueryParametersExtensions.BuildExpressionTree<QueryEntity>(filters).Compile();

        // Assert
        predicate(
                new QueryEntity
                {
                    Firstname = "John",
                    Lastname = "Doe"
                })
            .ShouldBeTrue();
        predicate(
                new QueryEntity
                {
                    Firstname = "John",
                    Lastname = "Smith"
                })
            .ShouldBeTrue();
        predicate(
                new QueryEntity
                {
                    Firstname = "John",
                    Lastname = "Jones"
                })
            .ShouldBeFalse();
        predicate(
                new QueryEntity
                {
                    Firstname = "Jane",
                    Lastname = "Doe"
                })
            .ShouldBeFalse();
    }

    [Fact]
    public void ResolvePropertyType_ShouldResolveADirectProperty()
    {
        // Arrange & Act
        var type = QueryParametersExtensions.ResolvePropertyType<QueryEntity>(nameof(QueryEntity.Firstname));

        // Assert
        type.ShouldBe(typeof(string));
    }

    [Fact]
    public void ResolvePropertyType_ShouldResolveANestedProperty()
    {
        // Arrange & Act
        var type = QueryParametersExtensions.ResolvePropertyType<QueryEntity>("Address.Country.Name");

        // Assert
        type.ShouldBe(typeof(string));
    }

    [Fact]
    public void ResolvePropertyType_ShouldThrowForAnUnknownProperty()
    {
        // Arrange & Act
        var exception = Record.Exception(
            () => QueryParametersExtensions.ResolvePropertyType<QueryEntity>("DoesNotExist"));

        // Assert
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("DoesNotExist");
    }

    [Fact]
    public void ResolvePropertyType_ShouldThrowForAnUnknownNestedProperty()
    {
        // Arrange & Act
        var exception = Record.Exception(
            () => QueryParametersExtensions.ResolvePropertyType<QueryEntity>("Address.DoesNotExist"));

        // Assert
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("DoesNotExist");
    }

    [Fact]
    public void GetValueExpression_ShouldDeserializeIntoThePropertyType()
    {
        // Arrange & Act
        var expression = QueryParametersExtensions.GetValueExpression<QueryEntity>(
            nameof(QueryEntity.Age),
            "30",
            Comparator.Equals);

        // Assert
        expression.Type.ShouldBe(typeof(int));
        Expression.Lambda<Func<int>>(expression).Compile()().ShouldBe(30);
    }

    [Fact]
    public void GetValueExpression_ShouldUseAListOfThePropertyTypeForIsOneOf()
    {
        // Arrange & Act
        var expression = QueryParametersExtensions.GetValueExpression<QueryEntity>(
            nameof(QueryEntity.TenantId),
            "[]",
            Comparator.IsOneOf);

        // Assert
        expression.Type.ShouldBe(typeof(List<Guid>));
    }

    [Fact]
    public void GetValueExpression_ShouldUseTheItemTypeForContains()
    {
        // Arrange & Act
        var expression = QueryParametersExtensions.GetValueExpression<QueryEntity>(
            nameof(QueryEntity.Tags),
            "\"admin\"",
            Comparator.Contains);

        // Assert
        expression.Type.ShouldBe(typeof(string));
    }

    [Fact]
    public void GetPropertyExpression_ShouldWalkTheWholePropertyPath()
    {
        // Arrange
        var parameter = Expression.Parameter(
            typeof(QueryEntity),
            "x");

        // Act
        var expression = QueryParametersExtensions.GetPropertyExpression(
            "Address.Country.Name",
            parameter);

        // Assert
        expression.Type.ShouldBe(typeof(string));
        MemberPathOf(expression).ShouldBe("Address.Country.Name");
    }

    [Fact]
    public void NestedPropertyOrField_ShouldResolveASingleMember()
    {
        // Arrange
        var parameter = Expression.Parameter(
            typeof(QueryEntity),
            "x");

        // Act
        var expression = QueryParametersExtensions.NestedPropertyOrField(
            parameter,
            new[] { "Address" });

        // Assert
        expression.Type.ShouldBe(typeof(QueryEntityAddress));
    }

    [Fact]
    public void NestedPropertyOrField_ShouldResolveNestedMembers()
    {
        // Arrange
        var parameter = Expression.Parameter(
            typeof(QueryEntity),
            "x");

        // Act
        var expression = QueryParametersExtensions.NestedPropertyOrField(
            parameter,
            new[] { "Address", "Country" });

        // Assert
        expression.Type.ShouldBe(typeof(QueryEntityCountry));
        MemberPathOf(expression).ShouldBe("Address.Country");
    }

    [Fact]
    public void AddPropertyNullCheckExpression_ShouldReturnTheTargetForADirectProperty()
    {
        // Arrange
        var parameter = Expression.Parameter(
            typeof(QueryEntity),
            "x");
        var target = Expression.Constant(true);

        // Act
        var expression = QueryParametersExtensions.AddPropertyNullCheckExpression(
            "Firstname",
            parameter,
            target);

        // Assert: nothing to guard against, so the target must be returned untouched
        expression.ShouldBeSameAs(target);
    }

    [Fact]
    public void AddPropertyNullCheckExpression_ShouldGuardEveryLevelOfANestedProperty()
    {
        // Arrange
        var parameter = Expression.Parameter(
            typeof(QueryEntity),
            "x");

        // Act
        var expression = QueryParametersExtensions.AddPropertyNullCheckExpression(
            "Address.Country.Name",
            parameter,
            Expression.Constant(true));

        // Assert: one null check per intermediate level, guarding against a NullReferenceException
        // when the compiled predicate walks the path
        var nullChecks = NullCheckedMemberPaths(expression);
        nullChecks.ShouldBe(
            new[] { "Address", "Address.Country" },
            ignoreOrder: true);
    }

    [Fact]
    public void GetExpressionTreeNodeIdExpressionBuilder_ShouldReturnAndAlsoForNodeIdsEndingWithZero()
    {
        // Arrange & Act: only the last digit selects the operator, and only 0 and 1 are legal
        var builder = QueryParametersExtensions.GetExpressionTreeNodeIdExpressionBuilder(10010);

        // Assert
        builder(
                Expression.Constant(true),
                Expression.Constant(false))
            .NodeType.ShouldBe(ExpressionType.AndAlso);
    }

    [Fact]
    public void GetExpressionTreeNodeIdExpressionBuilder_ShouldReturnOrElseForNodeIdsEndingWithOne()
    {
        // Arrange & Act
        var builder = QueryParametersExtensions.GetExpressionTreeNodeIdExpressionBuilder(10011);

        // Assert
        builder(
                Expression.Constant(true),
                Expression.Constant(false))
            .NodeType.ShouldBe(ExpressionType.OrElse);
    }

    [Fact]
    public void GetExpressionTreeNodeIdExpressionBuilder_ShouldThrowForUnsupportedIndicators()
    {
        // Arrange & Act
        var exception = Record.Exception(
            () => QueryParametersExtensions.GetExpressionTreeNodeIdExpressionBuilder(2));

        // Assert
        exception.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(Comparator.NotContains)]
    [InlineData(Comparator.EndsWith)]
    [InlineData(Comparator.GreaterThan)]
    [InlineData(Comparator.GreaterThanEquals)]
    [InlineData(Comparator.LowerThan)]
    [InlineData(Comparator.LowerThanEquals)]
    [InlineData(Comparator.Fuzzy)]
    [InlineData(Comparator.IsEmpty)]
    [InlineData(Comparator.IsNotEmpty)]
    public void GetComparatorExpressionBuilder_ShouldThrowForUnsupportedComparators(Comparator comparator)
    {
        // Arrange & Act
        var exception = Record.Exception(
            () => QueryParametersExtensions.GetComparatorExpressionBuilder<QueryEntity>(
                nameof(QueryEntity.Firstname),
                comparator));

        // Assert: note the asymmetry to the SQL builder, which does support IsEmpty and
        // IsNotEmpty via ARRAY_LENGTH - see CosmosQueryDefinitionTests
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain(comparator.ToString());
    }

    [Fact]
    public void GetComplexPropertyExpressionBuilder_ShouldThrowForUnsupportedKinds()
    {
        // Arrange & Act
        var exception = Record.Exception(
            () => QueryParametersExtensions.GetComplexPropertyExpressionBuilder(
                "ALL",
                typeof(QueryEntityVersion),
                Expression.Constant(new List<QueryEntityVersion>()),
                Expression.Constant(true)));

        // Assert
        exception.ShouldNotBeNull();
        exception.Message.ShouldContain("ALL");
    }

    [Fact]
    public void GetOrderByExpression_ShouldSelectTheSortProperty()
    {
        // Arrange
        var querySorting = new QuerySorting { OrderBy = "Firstname" };

        // Act
        var selector = querySorting.GetOrderByExpression<QueryEntity>().Compile();

        // Assert
        selector(new QueryEntity { Firstname = "John" }).ShouldBe("John");
    }

    [Fact]
    public void GetXPropertyExpression_ShouldSelectANestedProperty()
    {
        // Arrange & Act
        var selector = QueryParametersExtensions.GetXPropertyExpression<QueryEntity>("Address.City").Compile();

        // Assert
        selector(new QueryEntity { Address = new QueryEntityAddress { City = "Berlin" } }).ShouldBe("Berlin");
    }

    [Fact]
    public void GetSearchAfterExpression_ShouldCompareStringsWithCompareTo()
    {
        // Arrange
        var querySorting = new QuerySorting
        {
            OrderBy = "firstname",
            SearchAfter = "\"John\""
        };

        // Act
        var predicate = querySorting.GetSearchAfterExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Firstname = "Zoe" }).ShouldBeTrue();
        predicate(new QueryEntity { Firstname = "John" }).ShouldBeFalse();
        predicate(new QueryEntity { Firstname = "Anna" }).ShouldBeFalse();
    }

    [Fact]
    public void GetSearchAfterExpression_ShouldCompareNumbersWithCompareTo()
    {
        // Arrange
        var querySorting = new QuerySorting
        {
            OrderBy = "age",
            SearchAfter = "30"
        };

        // Act
        var predicate = querySorting.GetSearchAfterExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { Age = 31 }).ShouldBeTrue();
        predicate(new QueryEntity { Age = 30 }).ShouldBeFalse();
    }

    [Fact]
    public void GetSearchAfterExpression_ShouldCompareGuidsAsStrings()
    {
        // Arrange: Guid.CompareTo does not order like Cosmos does, so the builder falls back to string
        var querySorting = new QuerySorting
        {
            OrderBy = "tenantId",
            SearchAfter = "\"00000000-0000-0000-0000-000000000000\""
        };

        // Act
        var predicate = querySorting.GetSearchAfterExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { TenantId = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff") }).ShouldBeTrue();
        predicate(new QueryEntity { TenantId = Guid.Empty }).ShouldBeFalse();
    }

    [Fact]
    public void GetSearchAfterExpression_ShouldCompareDateTimesWithGreaterThan()
    {
        // Arrange
        var querySorting = new QuerySorting
        {
            OrderBy = "createdAt",
            SearchAfter = "\"2023-01-01T00:00:00Z\""
        };

        // Act
        var predicate = querySorting.GetSearchAfterExpression<QueryEntity>().Compile();

        // Assert
        predicate(new QueryEntity { CreatedAt = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc) })
            .ShouldBeTrue();
        predicate(new QueryEntity { CreatedAt = new DateTime(2022, 1, 1, 0, 0, 0, DateTimeKind.Utc) })
            .ShouldBeFalse();
    }

    /// <summary>
    ///     Walks a member access chain and returns its dotted path, e.g. "Address.Country".
    ///     Asserting on this instead of <c>Expression.ToString()</c> keeps the tests independent
    ///     of the runtime's expression debug view.
    /// </summary>
    private static string MemberPathOf(Expression expression)
    {
        var members = new List<string>();
        while (expression is MemberExpression memberExpression)
        {
            members.Insert(
                0,
                memberExpression.Member.Name);
            expression = memberExpression.Expression!;
        }

        return string.Join(
            ".",
            members);
    }

    /// <summary>
    ///     Collects the member paths of every <c>&lt;path&gt; != null</c> comparison in the tree.
    /// </summary>
    private static List<string> NullCheckedMemberPaths(Expression expression)
    {
        var paths = new List<string>();

        void Visit(Expression current)
        {
            if (current is not BinaryExpression binaryExpression)
            {
                return;
            }

            if (binaryExpression.NodeType == ExpressionType.NotEqual
                && binaryExpression.Right is ConstantExpression { Value: null })
            {
                paths.Add(MemberPathOf(binaryExpression.Left));
                return;
            }

            Visit(binaryExpression.Left);
            Visit(binaryExpression.Right);
        }

        Visit(expression);
        return paths;
    }

    private static QueryFilter Filter(
        string property,
        string value,
        Comparator comparator,
        int expressionTreeNodeId = 0)
    {
        return new QueryFilter
        {
            Property = property,
            Value = value,
            Comparator = comparator,
            ExpressionTreeNodeId = expressionTreeNodeId
        };
    }

    private static QueryParameters ParametersFor(params QueryFilter[] filters)
    {
        return new QueryParameters { Filters = new List<QueryFilter>(filters) };
    }
}
