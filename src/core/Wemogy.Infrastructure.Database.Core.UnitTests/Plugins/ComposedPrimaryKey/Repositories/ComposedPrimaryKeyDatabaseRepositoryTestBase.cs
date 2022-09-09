using System;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.Repositories;

public abstract partial class ComposedPrimaryKeyDatabaseRepositoryTestBase : IDisposable
{
    private readonly IServiceCollection _serviceCollection;
    protected IUserRepository UserRepository => _serviceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();

    protected ComposedPrimaryKeyDatabaseRepositoryTestBase(Action<IServiceCollection> addRepositoryAction)
    {
        _serviceCollection = new ServiceCollection();
        _serviceCollection.AddScoped<PrefixComposedPrimaryKeyBuilder>();
        addRepositoryAction(_serviceCollection);
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
    }

    public void Dispose()
    {
        // Cleanup
        // UserRepository.DeleteAsync(x => true).Wait();
    }

    protected void SetPrefixContext(string prefix)
    {
        _serviceCollection.AddSingleton(new PrefixContext(prefix));
    }
}
