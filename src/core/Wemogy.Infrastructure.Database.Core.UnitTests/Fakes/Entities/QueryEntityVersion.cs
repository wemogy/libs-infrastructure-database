namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

/// <summary>
///     Item type of <see cref="QueryEntity.Versions"/>.
/// </summary>
public class QueryEntityVersion
{
    public string Name { get; set; } = string.Empty;

    public QueryEntityCountry? Origin { get; set; }
}
