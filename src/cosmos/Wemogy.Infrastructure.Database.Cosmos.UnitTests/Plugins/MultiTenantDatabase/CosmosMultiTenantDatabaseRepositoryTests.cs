using System;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Repositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;
using Wemogy.Infrastructure.Database.Core.UnitTests.Providers;
using Wemogy.Infrastructure.Database.Cosmos.Factories;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Plugins.MultiTenantDatabase;

[Collection("Sequential")]
public class CosmosMultiTenantDatabaseRepositoryTests : MultiTenantDatabaseRepositoryTestsBase
{
    // TODO: Implement the wrapped methods with multi-tenant support and fix all tests.
    // TODO: Add more tests that cover edge cases for Multi-Tenancy
    public CosmosMultiTenantDatabaseRepositoryTests()
        : base(
            GetFactory()
        )
    {
    }

    private static Func<IDatabaseRepository<User>> GetFactory()
    {
        var dbFactory = CosmosDatabaseRepositoryFactory.CreateInstance<IMultiTenantUserRepository>( // TODO: Update this
            TestingConstants.ConnectionString,
            TestingConstants.DatabaseName,
            true);

        var multiTenantRepository = new MultiTenantDatabaseRepository<User>(
            dbFactory,
            new TestDatabaseTenantProvider());

        return () => multiTenantRepository;
    }
}
