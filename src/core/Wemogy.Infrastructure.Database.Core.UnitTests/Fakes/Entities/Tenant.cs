using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

public class Tenant : GlobalEntityBase
{
    public string Name { get; set; } = string.Empty;
}
