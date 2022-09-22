using System;
using Microsoft.Extensions.DependencyInjection;
using Wemogy.Infrastructure.Database.Core.Factories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.TestingData.Models;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.ComposedPrimaryKey.Repositories.AnimalEntity;

public abstract partial class ComposedPrimaryKeyDatabaseRepositoryTestBase : IDisposable
{
    private readonly IServiceCollection _serviceCollection;
    private IAnimalRepository AnimalRepository => _serviceCollection.BuildServiceProvider().GetRequiredService<IAnimalRepository>();

    protected ComposedPrimaryKeyDatabaseRepositoryTestBase(Action<IServiceCollection> addRepositoryAction)
    {
        _serviceCollection = new ServiceCollection();
        _serviceCollection.AddScoped<StringPrefixComposedPrimaryKeyBuilder>();
        addRepositoryAction(_serviceCollection);
        DatabaseRepositoryFactoryFactory.DatabaseClientProxy = null;
    }

    public void Dispose()
    {
        // Cleanup
        // AnimalRepository.DeleteAsync(x => true).Wait();
    }

    private void SetPrefixContext(string prefix)
    {
        _serviceCollection.AddSingleton(new PrefixContext(prefix));
    }
}
