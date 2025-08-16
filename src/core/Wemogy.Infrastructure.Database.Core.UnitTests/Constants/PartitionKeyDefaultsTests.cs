using Shouldly;
using Wemogy.Infrastructure.Database.Core.Constants;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Constants;

public class PartitionKeyDefaultsTests
{
    [Fact]
    public static void GlobalPartition_DefaultValue()
    {
        PartitionKeyDefaults.GlobalPartition.ShouldBe("global");
    }

    [Fact]
    public static void CustomizeGlobalPartition_WhenCalled_ThenCustomizesGlobalPartition()
    {
        var tenantA = new Tenant();
        PartitionKeyDefaults.CustomizeGlobalPartition("custom");
        var tenantB = new Tenant();

        PartitionKeyDefaults.GlobalPartition.ShouldBe("custom");
        tenantA.PartitionKey.ShouldBe("global");
        tenantB.PartitionKey.ShouldBe("custom");

        // Clean up
        PartitionKeyDefaults.CustomizeGlobalPartition("global");
    }
}
