using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Mongo.UnitTests.Constants;

namespace Wemogy.Infrastructure.Database.Mongo.UnitTests.Abstractions;

public abstract class MongoUnitTestBase
{
    protected IServiceCollection ServiceCollection { get; }
    protected string ConnectionString { get; }
    protected string DatabaseName { get; }

    protected MongoUnitTestBase()
    {
        ServiceCollection = new ServiceCollection();
        ConnectionString = TestingConstants.ConnectionString;
        DatabaseName = TestingConstants.DatabaseName;
    }
}
