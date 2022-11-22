namespace Wemogy.Infrastructure.Database.Core.Plugins.MultiTenantDatabase.Abstractions;

public interface IDatabaseTenantProvider
{
    string GetTenantId();
}
