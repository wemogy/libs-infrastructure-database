using System.Collections.Generic;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;

/// <summary>
///     Nested reference type of <see cref="QueryEntity"/>.
/// </summary>
public class QueryEntityAddress
{
    public string City { get; set; } = string.Empty;

    public QueryEntityCountry? Country { get; set; }

    public List<QueryEntityVersion> Versions { get; set; } = new List<QueryEntityVersion>();
}
