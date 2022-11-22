using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Providers;

public class AppleTenantProvider : IDatabaseTenantProvider
{
    public string GetTenantId() => "apple_production";
}
