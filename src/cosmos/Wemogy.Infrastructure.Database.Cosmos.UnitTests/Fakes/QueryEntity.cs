using System;
using System.Collections.Generic;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;

/// <summary>
///     Plain fake used by the query expression builder tests. It intentionally covers every
///     property shape the builders branch on: scalars, GUIDs, dates, collections of scalars,
///     nested reference types (for the null-check expressions) and collections of complex types
///     (for the <c>property&lt;ANY&gt;subProperty</c> syntax).
/// </summary>
public class QueryEntity
{
    public string Firstname { get; set; } = string.Empty;

    public string? Lastname { get; set; }

    public int Age { get; set; }

    public Guid TenantId { get; set; }

    public DateTime CreatedAt { get; set; }

    public List<string> Tags { get; set; } = new List<string>();

    public List<Guid> GroupIds { get; set; } = new List<Guid>();

    public QueryEntityAddress? Address { get; set; }

    public List<QueryEntityVersion> Versions { get; set; } = new List<QueryEntityVersion>();
}
