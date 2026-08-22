using System.Linq;
using Shouldly;
using Wemogy.Infrastructure.Database.Cosmos.Models;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Models;

public class QueryDefinitionFilterConditionTests
{
    [Fact]
    public void Constructor_ShouldStartWithoutFilterAndParameters()
    {
        // Arrange & Act
        var condition = new QueryDefinitionFilterCondition();

        // Assert
        condition.QueryText.ShouldBeEmpty();
        condition.HasFilter.ShouldBeFalse();
        condition.Parameters.ShouldBeEmpty();
    }

    [Fact]
    public void And_ShouldNotPrefixTheOperatorForTheFirstCondition()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.And("c.firstname = \"John\"");

        // Assert
        condition.QueryText.ShouldBe("c.firstname = \"John\" ");
        condition.HasFilter.ShouldBeTrue();
    }

    [Fact]
    public void And_ShouldChainFollowUpConditionsWithAnd()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.And("c.firstname = \"John\"");
        condition.And("c.age = 30");

        // Assert
        condition.QueryText.ShouldBe("c.firstname = \"John\" AND c.age = 30 ");
    }

    [Fact]
    public void Or_ShouldChainFollowUpConditionsWithOr()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.Or("c.firstname = \"John\"");
        condition.Or("c.firstname = \"Jane\"");

        // Assert
        condition.QueryText.ShouldBe("c.firstname = \"John\" OR c.firstname = \"Jane\" ");
    }

    [Fact]
    public void Comma_ShouldChainFollowUpStatementsWithComma()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.Comma("c.firstname ASC");
        condition.Comma("c.age DESC");

        // Assert
        condition.QueryText.ShouldBe("c.firstname ASC , c.age DESC ");
    }

    [Fact]
    public void And_WithBrackets_ShouldWrapTheConditionInBrackets()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.And(
            "c.firstname = \"John\"",
            true);

        // Assert
        condition.QueryText.ShouldBe("(c.firstname = \"John\") ");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void And_ShouldIgnoreBlankConditions(string blankCondition)
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.And(blankCondition);

        // Assert
        condition.QueryText.ShouldBeEmpty();
        condition.HasFilter.ShouldBeFalse();
    }

    [Fact]
    public void And_WithParameter_ShouldReplaceTheParameterPlaceholder()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.And(
            "c.firstname = @paramHere",
            "John");

        // Assert
        condition.QueryText.ShouldNotContain("@paramHere");
        condition.Parameters.Count.ShouldBe(1);
        var parameter = condition.Parameters.Single();
        parameter.Key.ShouldStartWith("@param");
        parameter.Key.ShouldEndWith("_0");
        parameter.Value.ShouldBe("John");
        condition.QueryText.ShouldBe($"c.firstname = {parameter.Key} ");
    }

    [Fact]
    public void And_WithParameter_ShouldUseAnIncrementingParameterIndex()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.And(
            "c.firstname = @paramHere",
            "John");
        condition.And(
            "c.age = @paramHere",
            30);

        // Assert
        condition.Parameters.Count.ShouldBe(2);
        condition.Parameters.Keys.ShouldContain(x => x.EndsWith("_0"));
        condition.Parameters.Keys.ShouldContain(x => x.EndsWith("_1"));
    }

    [Fact]
    public void And_WithParameter_ShouldUseAnInstanceSpecificParameterNamespace()
    {
        // Arrange
        var conditionA = new QueryDefinitionFilterCondition();
        var conditionB = new QueryDefinitionFilterCondition();

        // Act: both use index 0, so only the namespace can keep them apart
        conditionA.And(
            "c.firstname = @paramHere",
            "John");
        conditionB.And(
            "c.firstname = @paramHere",
            "Jane");

        // Assert: otherwise merging two conditions would silently drop a parameter
        conditionA.Parameters.Single().Key.ShouldNotBe(conditionB.Parameters.Single().Key);
    }

    [Fact]
    public void Or_WithParameter_ShouldChainWithOr()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();

        // Act
        condition.Or(
            "c.firstname = @paramHere",
            "John");
        condition.Or(
            "c.firstname = @paramHere",
            "Jane");

        // Assert
        condition.QueryText.ShouldContain(" OR ");
        condition.Parameters.Values.ShouldBe(
            new object[] { "John", "Jane" },
            ignoreOrder: true);
    }

    [Fact]
    public void ReplaceGreaterThanWithEquals_ShouldRewriteAllGreaterThanOperators()
    {
        // Arrange
        var condition = new QueryDefinitionFilterCondition();
        condition.And("c.firstname > \"John\"");
        condition.And("c.age > 30");

        // Act
        condition.ReplaceGreaterThanWithEquals();

        // Assert: this is how the search-after tie-breaker chain is built
        condition.QueryText.ShouldBe("c.firstname = \"John\" AND c.age = 30 ");
    }

    [Fact]
    public void MergeParameters_ShouldCopyParametersOfTheOtherCondition()
    {
        // Arrange
        var target = new QueryDefinitionFilterCondition();
        target.And(
            "c.firstname = @paramHere",
            "John");
        var source = new QueryDefinitionFilterCondition();
        source.And(
            "c.age = @paramHere",
            30);

        // Act
        target.MergeParameters(source);

        // Assert
        target.Parameters.Count.ShouldBe(2);
        target.Parameters.Values.ShouldContain("John");
        target.Parameters.Values.ShouldContain(30);
    }

    [Fact]
    public void MergeParameters_ShouldKeepTheExistingValueOnKeyCollision()
    {
        // Arrange
        var target = new QueryDefinitionFilterCondition();
        target.And(
            "c.firstname = @paramHere",
            "John");
        var key = target.Parameters.Single().Key;
        var source = new QueryDefinitionFilterCondition();
        source.Parameters.Add(
            key,
            "Jane");

        // Act
        target.MergeParameters(source);

        // Assert
        target.Parameters.Count.ShouldBe(1);
        target.Parameters[key].ShouldBe("John");
    }

    [Fact]
    public void MergeParameters_ShouldNotChangeTheQueryText()
    {
        // Arrange
        var target = new QueryDefinitionFilterCondition();
        target.And("c.firstname = \"John\"");
        var source = new QueryDefinitionFilterCondition();
        source.And(
            "c.age = @paramHere",
            30);

        // Act
        target.MergeParameters(source);

        // Assert
        target.QueryText.ShouldBe("c.firstname = \"John\" ");
    }
}
