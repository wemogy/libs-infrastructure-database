using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Setup;
using Wemogy.Infrastructure.Database.InMemory.Factories;

namespace Wemogy.Infrastructure.Database.InMemory.Setup
{
    public static class DependencyInjection
    {
        public static DatabaseSetupEnvironment AddInMemoryDatabaseClient(this IServiceCollection serviceCollection)
        {
            return new DatabaseSetupEnvironment(
                serviceCollection,
                new InMemoryDatabaseClientFactory());
        }
    }
}
