using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Setup;

public static class DependencyInjection
{
    public static DatabaseSetupEnvironment AddDatabase(this IServiceCollection serviceCollection, IDatabaseClientFactory databaseClientFactory)
    {
        return new DatabaseSetupEnvironment(serviceCollection, databaseClientFactory);
    }
}
