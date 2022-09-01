using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Abstractions;

public abstract class CosmosUnitTestBase
{
    protected IServiceCollection ServiceCollection { get; }
    protected string ConnectionString { get; }
    protected string DatabaseName { get; }

    protected CosmosUnitTestBase()
    {
        ServiceCollection = new ServiceCollection();
        ConnectionString = TestingConstants.ConnectionString;
        DatabaseName = TestingConstants.DatabaseName;
    }
}
