using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Providers;

public class DataCenterTenantProvider : IDatabaseTenantProvider
{
    public string GetTenantId() => "datacenter-staging";
}
