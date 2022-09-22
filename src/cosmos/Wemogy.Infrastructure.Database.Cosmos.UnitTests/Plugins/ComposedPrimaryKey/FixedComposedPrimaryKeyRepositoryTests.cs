using Microsoft.Extensions.DependencyInjection;
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

            serviceCollection
                .AddCosmosDatabase(TestingConstants.ConnectionString, TestingConstants.DatabaseName, true)
                .AddRepository<IUserRepository, PrefixComposedPrimaryKeyBuilder>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            return serviceProvider.GetRequiredService<IUserRepository>();
        })
    {
    }
}
