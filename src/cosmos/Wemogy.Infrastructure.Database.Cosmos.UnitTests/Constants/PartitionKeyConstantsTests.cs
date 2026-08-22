using Shouldly;
using Wemogy.Infrastructure.Database.Cosmos.Constants;
using Xunit;
using CosmosPartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;

public class PartitionKeyConstantsTests
{
    [Fact]
    public void Global_ShouldUseTheGlobalPartitionKeyValue()
    {
        // Arrange & Act
        var global = PartitionKey.Global;

        // Assert
        PartitionKeyValue.Global.ShouldBe("global");
        global.CosmosPartitionKey.ShouldBe(new CosmosPartitionKey("global"));
    }
}
