using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.InMemory.Factories;

namespace Wemogy.Infrastructure.Database.InMemory.Setup
{
    public static class DependencyInjection
    {
        public static InMemorySetupEnvironment AddInMemoryDatabaseClient(this IServiceCollection serviceCollection)
        {
            return new InMemorySetupEnvironment(new InMemoryDatabaseRepositoryFactory(serviceCollection));
        }
    }
}
