# Migrating to v5

Version 5 makes two breaking changes. Both are source-level: **no stored document changes
shape, and no data migration is needed.**

- [Serialization moves to `System.Text.Json`](#serialization-moves-to-systemtextjson)
- [`CreatedAt` and `UpdatedAt` become `DateTimeOffset`](#createdat-and-updatedat-become-datetimeoffset)

## Serialization moves to `System.Text.Json`

Everything this library puts on the wire - entities, query parameters and patch values - is now
serialized with `System.Text.Json`. The Cosmos DB SDK still uses `Newtonsoft.Json` internally for
its own request and response types, so the package is still in the dependency tree; nothing this
library writes goes through it.

The stored format is unchanged: camelCase property names, null values omitted, and non-ASCII
characters left unescaped rather than written as `\uXXXX`.

### Replace the Newtonsoft attributes on your entities

This is the change most consumers have to make. A `Newtonsoft.Json` attribute is silently ignored
by `System.Text.Json`, so a property that used to be persisted under a name of its own would start
being written under its camelCased member name instead - against an existing container, that
writes to a new field and reads back a default.

| Before                              | After                                        |
| ----------------------------------- | -------------------------------------------- |
| `[JsonProperty("customLabel")]`     | `[JsonPropertyName("customLabel")]`          |
| `[JsonIgnore]` (`Newtonsoft.Json`)  | `[JsonIgnore]` (`System.Text.Json.Serialization`) |
| `[JsonConverter(typeof(MyConverter))]` | a `JsonConverter<T>` and the same attribute from `System.Text.Json.Serialization` |

```csharp title="User.cs"
using System.Text.Json.Serialization;
using Wemogy.Infrastructure.Database.Core.Abstractions;

public class User : EntityBase
{
    [JsonPropertyName("customLabel")]
    public string Label { get; set; } = string.Empty;
}
```

### Give every entity a public parameterless constructor

`Newtonsoft.Json` could construct an entity through a non-public constructor. `System.Text.Json`
cannot: it needs a public parameterless constructor, a single parameterized one, or one annotated
with `[JsonConstructor]`. An entity without any of those throws `NotSupportedException` on the
first read.

### Registering a converter of your own

The serializer is configurable, so an entity that needs a converter no longer has to fight the
defaults. Start from the options the package configures, add to them, and pass the result to the
client:

```csharp
var options = CosmosEntitySerializer.CreateDefaultOptions();
options.Converters.Add(new MyConverter());

var cosmosClient = new CosmosClient(
    connectionString,
    new CosmosClientOptions { Serializer = new CosmosEntitySerializer(options) });

services.AddCosmosDatabase(cosmosClient, databaseName);
```

Starting from `CreateDefaultOptions()` matters: it carries the camelCase naming that
`SerializeMemberName` reports back for LINQ queries and patch paths, and the `[ETag]` rules.

### Smaller signature changes

- `QueryParametersExtensions.GetCount` returns a `FeedIterator<JsonObject>` rather than a
  `FeedIterator<JObject>`.
- `MappingMetadata.Deserialize` returns plain CLR values rather than `JToken`s, and a JSON array
  now comes back as a `List<object?>`. `MappingMetadata.DeserializeArray` is new, and returns one
  value per element.
- `ETagContractResolver` is gone. The `[ETag]` rules are applied by a `JsonTypeInfo` modifier
  instead; nothing about how an entity declares its eTag changes.

### What stays as forgiving as it was

The JSON a caller writes into `QueryFilter.Value` and `QuerySorting.SearchAfter` is read with
options that stay as lenient as the `Newtonsoft.Json` reader they replace: an enum is accepted
under its name as well as its number, a number is accepted inside a string, a property name is
matched case insensitively, and a date is accepted in the spellings `System.Text.Json` rejects on
its own (`2026-08-25 10:00:00`, `08/25/2026`). Query building has no `try`/`catch` around it, so a
spelling that stopped being accepted would throw at the call site rather than filter nothing.

A date spelling that carries **no** offset is still read in the zone of the running machine, as it
was in v4. Spell a filter value or a cursor with an explicit offset if you want it to mean the same
thing everywhere.

### Cursors over a timestamp keep working

A `searchAfter` cursor is compared against the stored string, so it only matches while it is
spelled the way the document was written. A value that parses as a timestamp is therefore
re-spelled by the same converter that writes the entity - a cursor built with
`JsonSerializer.Serialize(user.UpdatedAt)`, which produces `2026-08-25T10:00:00+00:00`, reaches
Cosmos DB as `2026-08-25T10:00:00Z`. Without that, `+` (0x2B) sorting before `Z` (0x5A) would make
an ascending page hand back the last row of the previous one.

## `CreatedAt` and `UpdatedAt` become `DateTimeOffset`

```csharp
[UtcDateTimeOffset]
public DateTimeOffset CreatedAt { get; set; }

[UtcDateTimeOffset]
public DateTimeOffset UpdatedAt { get; set; }
```

Both are always UTC and always written by the library. `DateTime` was the wrong type for that,
because its `Kind` is not part of its value: a `Utc` 10:00 and a `Local` 10:00 compare equal while
being different instants, and a stored timestamp deserialized into the reading host's zone reads
differently in Berlin than in a UTC container.

### What you have to change

For an entity deriving from `EntityBase`, this is a recompile. Only three things need touching:

- an entity implementing `IEntityBase` **directly** has to change the two property types
- code assigning a `DateTime` to either field - the implicit conversion compiles, but it takes the
  offset from the *host*, so pass a `DateTimeOffset` explicitly
- code reading either field into a `DateTime` - use `.UtcDateTime`

```csharp
// before
DateTime lastWrite = user.UpdatedAt;

// after
DateTime lastWrite = user.UpdatedAt.UtcDateTime;
```

### Reading documents written before the upgrade

`EntityBase` marks both timestamps with `[UtcDateTimeOffset]`, so **no existing document needs
migrating**. The attribute handles the shapes a `DateTime` was stored in:

| Stored value | Read as |
| --- | --- |
| `"2026-08-25T10:00:00Z"` (`Kind.Utc`) | `2026-08-25T10:00:00 +00:00` |
| `"2026-08-25T12:00:00+02:00"` (`Kind.Local`) | `2026-08-25T12:00:00 +02:00`, offset kept |
| `"2026-08-25T10:00:00"` (`Kind.Unspecified`) | `2026-08-25T10:00:00 +00:00` |
| `1787652000000` (epoch ms, the shape `Wemogy.Core` writes a `DateTime` in) | `2026-08-25T10:00:00 +00:00` |

The third row is the one that matters most, and it is **not** an exception you would have noticed.
A `DateTime` whose `Kind` was `Unspecified` was stored without any offset at all, and
`System.Text.Json` reads such a value into the offset of the *reading machine* — so the same
document would mean 10:00 UTC in a container and 08:00 UTC on a developer's machine in Berlin.
The attribute takes it as UTC, which is what the library always meant it to be.

Because it is a property attribute rather than a converter on the options, it holds wherever the
entity is deserialized — including through your own `JsonSerializerOptions` and through
`Wemogy.Core`'s `Clone()`, neither of which knows about this library's serializer.

**If you implement `IEntityBase` directly** rather than deriving from `EntityBase`, put it on both
properties yourself:

```csharp
using Wemogy.Infrastructure.Database.Core.Attributes;

[UtcDateTimeOffset]
public DateTimeOffset CreatedAt { get; set; }

[UtcDateTimeOffset]
public DateTimeOffset UpdatedAt { get; set; }
```

It is also worth putting on any `DateTimeOffset` of your own whose documents were written from a
`DateTime`, for exactly the same reason.

### Why your existing documents are safe

`System.Text.Json` writes a `DateTimeOffset` as `2026-08-25T10:00:00+00:00`, where a UTC
`DateTime` was written as `2026-08-25T10:00:00Z`. Both parse back into either type, so reading an
existing document is safe either way - but Cosmos DB compares and orders a timestamp as the
**string** it is stored as, and `+` (0x2B) sorts before `Z` (0x5A). A container holding both
spellings would stop ordering correctly by either field, which would break every range filter and
every `searchAfter` cursor over `UpdatedAt`.

The library therefore keeps writing the `...Z` form for a zero offset, byte for byte identical to
what v4 wrote. An offset you deliberately store is left as it is, because normalizing it away
would be a silent loss rather than a compatibility fix.
