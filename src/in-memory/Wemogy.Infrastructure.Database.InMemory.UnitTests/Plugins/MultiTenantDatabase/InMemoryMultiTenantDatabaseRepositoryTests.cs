using System;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.InMemory.Factories;
using Xunit;

namespace Wemogy.Infrastructure.Database.InMemory.UnitTests.Plugins.MultiTenantDatabase;

[Collection("Sequential")]
public class InMemoryMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
{
    public InMemoryMultiTenantDatabaseRepositoryTests()
        : base(
            GetFactory(new MicrosoftTenantProvider()),
            GetFactory(new AppleTenantProvider()))
    {
    }

    private static Func<IDatabaseRepository<User>> GetFactory(IDatabaseTenantProvider provider)
    {
        return () =>
        {
            var databaseRepository = InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>();

            var multiTenantRepository = new MultiTenantDatabaseRepository<User>(
                databaseRepository,
                provider);

            return multiTenantRepository;
        };
    }
}
