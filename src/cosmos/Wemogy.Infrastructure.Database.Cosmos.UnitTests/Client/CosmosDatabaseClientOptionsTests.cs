using Shouldly;
using Wemogy.Infrastructure.Database.Cosmos.Client;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Client;

public class CosmosDatabaseClientOptionsTests
{
    [Fact]
    public void Constructor_ShouldExposeDatabaseAndContainerName()
    {
        // Arrange & Act
        var options = new CosmosDatabaseClientOptions(
            "my-database",
            "users");

        // Assert
        options.DatabaseName.ShouldBe("my-database");
        options.ContainerName.ShouldBe("users");
    }
}
