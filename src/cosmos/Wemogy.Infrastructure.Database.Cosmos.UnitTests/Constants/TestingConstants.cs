using Wemogy.Configuration;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;

public static class TestingConstants
{
    public static string ConnectionString
    {
        get
        {
            var configuration = ConfigurationFactory.BuildConfiguration();
            return configuration["COSMOS_CONNECTION_STRING"];
        }
    }

    public static string DatabaseName
    {
        get
        {
            var configuration = ConfigurationFactory.BuildConfiguration();
            return configuration["COSMOS_DATABASE_NAME"];
        }
    }
}
