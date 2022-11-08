using Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Providers;

public class MicrosoftTenantProvider : IDatabaseTenantProvider
{
    public string GetTenantId() => "microsoft-staging";
}
