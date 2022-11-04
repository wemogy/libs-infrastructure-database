using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Providers;

public class TestDatabaseTenantProvider : IDatabaseTenantProvider
{
    public string GetTenantId() => "snaatch_staging";
}
