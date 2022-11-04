using System;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.UnitTests.DatabaseRepositories;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Plugins.MultiTenantDatabase;

public abstract class MultiTenantDatabaseRepositoryTestsBase : RepositoryTestBase
{
    protected MultiTenantDatabaseRepositoryTestsBase(Func<IDatabaseRepository<User>> multiTenantUserRepositoryFactory)
        : base(multiTenantUserRepositoryFactory)
    {
    }
}
