using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.Cosmos.Setup;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.ComposedPrimaryKey;

[Collection("Sequential")]
public class FixedComposedPrimaryKeyRepositoryTests : RepositoryTestBase
{
    public FixedComposedPrimaryKeyRepositoryTests()
        : base(() =>
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddScoped<PrefixComposedPrimaryKeyBuilder>();
            serviceCollection.AddSingleton(new PrefixContext("tenantA"));

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetRequiredService<ILogger>();

            serviceCollection
                .AddCosmosDatabase(TestingConstants.ConnectionString, TestingConstants.DatabaseName, logger, true)
                .AddRepository<IUserRepository, PrefixComposedPrimaryKeyBuilder>();

            return serviceProvider.GetRequiredService<IUserRepository>();
        })
    {
    }
}
