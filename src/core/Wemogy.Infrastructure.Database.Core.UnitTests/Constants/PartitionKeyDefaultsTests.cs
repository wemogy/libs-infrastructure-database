using Wemogy.Infrastructure.Database.Core.Constants;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Constants;

public class PartitionKeyDefaultsTests
{
    [Fact]
    public static void GlobalPartition_DefaultValue()
    {
        Assert.Equal("global", PartitionKeyDefaults.GlobalPartition);
    }

    [Fact]
    public static void CustomizeGlobalPartition_WhenCalled_ThenCustomizesGlobalPartition()
    {
        var tenantA = new Tenant();
        PartitionKeyDefaults.CustomizeGlobalPartition("custom");
        var tenantB = new Tenant();

        Assert.Equal("custom", PartitionKeyDefaults.GlobalPartition);
        Assert.Equal("global", tenantA.PartitionKey);
        Assert.Equal("custom", tenantB.PartitionKey);

        // Clean up
        PartitionKeyDefaults.CustomizeGlobalPartition("global");
    }
}
