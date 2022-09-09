using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;
using Wemogy.Infrastructure.Database.InMemory.Setup;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Plugins.ComposedPrimaryKey;

public class FixedComposedPrimaryKeyRepositoryTests : RepositoryTestBase
{
    public FixedComposedPrimaryKeyRepositoryTests()
        : base(() =>
        {
            var serviceCollection = new ServiceCollection();

            serviceCollection.AddScoped<PrefixComposedPrimaryKeyBuilder>();
            serviceCollection.AddSingleton(new PrefixContext("tenantA"));

            serviceCollection
                .AddInMemoryDatabaseClient()
                .AddRepository<IUserRepository, PrefixComposedPrimaryKeyBuilder>();

            var serviceProvider = serviceCollection.BuildServiceProvider();

            return serviceProvider.GetRequiredService<IUserRepository>();
        })
    {
    }
}
