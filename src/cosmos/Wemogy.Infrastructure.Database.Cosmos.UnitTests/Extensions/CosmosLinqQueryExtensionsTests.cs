using Shouldly;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Extensions;

public class CosmosLinqQueryExtensionsTests
{
    [Fact]
    public void ExtractWhereFragment_ShouldReturnTheCondition()
    {
        // Arrange: the LINQ provider returns the query as a JSON document
        const string querySql = "{\"query\":\"SELECT VALUE root FROM root WHERE (root[\\\"credits\\\"] < 5)\"}";

        // Act
        var fragment = CosmosLinqQueryExtensions.ExtractWhereFragment(querySql);

        // Assert: the root alias becomes c and the escaped quotes are unescaped
        fragment.ShouldBe("(c[\"credits\"] < 5)");
    }

    [Fact]
    public void ExtractWhereFragment_ShouldIgnoreTheWordInAStringConstant()
    {
        // Arrange: a constant of the condition carries the word WHERE
        const string querySql =
            "{\"query\":\"SELECT VALUE root FROM root WHERE (root[\\\"firstname\\\"] = \\\"SOMEWHERE\\\")\"}";

        // Act
        var fragment = CosmosLinqQueryExtensions.ExtractWhereFragment(querySql);

        // Assert: the whole condition, not the tail of the constant
        fragment.ShouldBe("(c[\"firstname\"] = \"SOMEWHERE\")");
    }

    [Fact]
    public void ExtractWhereFragment_ShouldReturnNullWithoutACondition()
    {
        // Act & Assert
        CosmosLinqQueryExtensions.ExtractWhereFragment("{\"query\":\"SELECT VALUE root FROM root\"}").ShouldBeNull();
        CosmosLinqQueryExtensions.ExtractWhereFragment(null).ShouldBeNull();
        CosmosLinqQueryExtensions.ExtractWhereFragment(string.Empty).ShouldBeNull();
    }
}
