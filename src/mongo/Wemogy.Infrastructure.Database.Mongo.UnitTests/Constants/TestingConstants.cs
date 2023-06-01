using Wemogy.Configuration;

namespace Wemogy.Infrastructure.Database.Mongo.UnitTests.Constants;

public static class TestingConstants
{
    public static string ConnectionString
    {
        get
        {
            var configuration = ConfigurationFactory.BuildConfiguration();
            return configuration["MONGO_CONNECTION_STRING"]!;
        }
    }

    public static string DatabaseName
    {
        get
        {
            var configuration = ConfigurationFactory.BuildConfiguration();
            return configuration["MONGO_DATABASE_NAME"]!;
        }
    }
}
