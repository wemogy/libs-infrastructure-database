# Attributes

The library uses attributes to describe the role of entity properties and to
configure repositories. All attributes live in the
`Wemogy.Infrastructure.Database.Core.Attributes` namespace.

## Entity property attributes

These attributes are applied to properties of an entity class.

| Attribute          | Target   | Required | Description                                                                 |
| ------------------ | -------- | -------- | --------------------------------------------------------------------------- |
| `[Id]`             | Property | Yes      | Marks the property that holds the unique identifier of the entity. Must be a `string`. |
| `[PartitionKey]`   | Property | Yes      | Marks the property used as the partition key (see [Getting Started](./02-getting-started.md#partition-key)). |
| `[HierarchicalPartitionKey]` | Property | Yes      | Marks one component of a [hierarchical partition key](./14-hierarchical-partition-keys.md). Used instead of `[PartitionKey]`, on every property forming the key. |
| `[SoftDeleteFlag]` | Property | No       | Marks the `bool` property that flags an entity as soft-deleted (see [Soft Delete](./07-soft-delete.md)). |
| `[ETag]`           | Property | No       | Opts the entity into optimistic concurrency (see [Optimistic Concurrency](./09-optimistic-concurrency.md)). |
| `[FixedPoint]`     | Property, Field | No | Persists a `decimal` as an exact scaled integer, which is what makes an atomic increment of it possible (see below). |
| `[UtcDateTimeOffset]` | Property | No    | Reads a `DateTimeOffset` whose document was written from a `DateTime` as UTC rather than in the zone of the reading machine, and writes a zero offset as `...Z` (see [Migrating to v5](./16-migrating-to-v5.md#reading-documents-written-before-the-upgrade)). |

An entity declares its partition key with **either** `[PartitionKey]` or
`[HierarchicalPartitionKey]`, never with both.

When you derive from `EntityBase`, `[Id]` and `[SoftDeleteFlag]` are already
provided, and both timestamps already carry `[UtcDateTimeOffset]`.
`GlobalEntityBase` additionally provides a global `[PartitionKey]`.

```csharp title="Entity using property attributes"
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

public class User : EntityBase // provides [Id] Id and [SoftDeleteFlag] IsDeleted
{
    [PartitionKey]
    public string TenantId { get; set; } = string.Empty;

    public string Firstname { get; set; } = string.Empty;
}
```

### `[FixedPoint]`

A `decimal` written as a floating point number is at the mercy of the number type of the
database. Cosmos DB stores every number as IEEE 754 binary64, and its `Increment` takes a
`long` or a `double` - so a base-10 domain like money or a metered quota has no exact
atomic increment at all.

`[FixedPoint(Scale = n)]` moves such a member into whole units of `10^-n`: the document
carries the integer `value * 10^n`, and the entity reads the decimal back by dividing by
the same factor. `0.5m` at scale 6 is stored as `500000`, an increment of `0.5m` becomes
`incr /value 500000`, and a condition or filter comparing against `100m` compares against
`100000000`.

```csharp title="A quota balance that can be incremented exactly"
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

public class QuotaBalance : GlobalEntityBase
{
    [FixedPoint(Scale = 6)]
    public decimal Value { get; set; }
}

await repository.PatchAsync(
    id,
    partitionKey,
    p => p.Increment(x => x.Value, 0.5m),
    condition: x => x.Value <= 100m);
```

| Parameter | Type  | Description                                                                                  |
| --------- | ----- | -------------------------------------------------------------------------------------------- |
| `Scale`   | `int` | The number of decimal places the member is stored with, between 0 and 18. Scale 6 stores `0.5` as `500000`. |

**What it applies to.** Only a `decimal` or a `decimal?`, on a property or a field, at any
depth of the entity - a member of a nested object or of a collection item is scaled the same
way. Putting it on any other type throws `FixedPointMemberIsNotADecimal` the first time the
member is read.

**The exact range.** Exactness holds while the *scaled* value stays inside ±(2^53 − 1), which at
scale 6 is roughly ±9.0 × 10⁹ in domain units. Two different things happen at that bound, and the
difference matters:

- **A value you hand over is checked.** The value of a create, a replace, an upsert or a `Set`, and
  the *operand* of an `Increment`, are refused with `FixedPointValueOutOfRange` rather than
  silently degraded.
- **The accumulated result of an increment is yours to keep in range.** An atomic increment is
  applied by the database without reading the current value first, so nothing can pre-check what
  it adds up to — a counter incremented by in-range operands can still cross the bound. It is
  caught on the next read instead, with `FixedPointStoredValueOutOfRange`, rather than handing back
  a value only approximately equal to what the increments added up to. Size the counter so it
  cannot get there.

**No silent rounding.** A value carrying more decimal places than the declared scale is refused
with `FixedPointPrecisionExceeded` on every write path - a create, a replace, an upsert, a `Set`
and an `Increment` alike, and by both providers. Round it yourself before writing it. Because of
that rule a stored value is always exactly the scaled integer divided by `10^Scale`, which is
what lets the Cosmos and the in-memory provider agree on what is stored. The check walks the entity
by declared type — properties, fields, collection elements and dictionary values — so a member
reached only through an `object` reference is scaled by the serializer but not checked up front.

**Queries and conditions scale with the value.** A patch condition, a query predicate and a
`QueryParameters` filter on a fixed-point member are all rewritten against the scaled integer, so
`x => x.Value <= 100m` asks the question you wrote. Sorting is unaffected - a scaled integer
orders like the value it encodes. A predicate the rewrite cannot express against the stored value -
comparing two members of different scales, comparing against another field of the document, a
conversion out of `decimal`, or a construct like `list.Contains(x.Value)` - is refused with
`FixedPointExpressionNotSupported` instead of quietly answering a different question. So is an
access the rewrite cannot reach at all — inside a nested lambda or behind an indexer, e.g.
`x => x.Items.Any(i => i.Balance > 1m)`; filter such a collection in memory, or keep the member on
the entity the query addresses.

**Adding it to an existing container needs a migration.** The attribute changes how the member is
read as well as written. A document written before it was added carries the unscaled value, and
reading it throws `FixedPointStoredValueIsNotScaled` rather than handing out a value 10^Scale times
too small.

## Repository attributes

These attributes are applied to the repository interface.

### `[RepositoryOptions]`

Customizes repository behavior.

| Parameter          | Type     | Default | Description                                                          |
| ------------------ | -------- | ------- | -------------------------------------------------------------------- |
| `enableSoftDelete` | `bool`   | `false` | Enables [soft delete](./07-soft-delete.md) for the repository.       |
| `collectionName`   | `string?`| `null`  | Overrides the collection/container name (defaults to the entity name). |

```csharp title="IUserRepository.cs"
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

[RepositoryOptions(enableSoftDelete: true, collectionName: "users")]
public interface IUserRepository : IDatabaseRepository<User>
{
}
```

### `[RepositoryReadFilter]`

Registers one or more [read filters](./08-filters.md#read-filters) that are applied
to every read operation. Can be specified multiple times.

```csharp
[RepositoryReadFilter(typeof(GeneralUserReadFilter))]
public interface IUserRepository : IDatabaseRepository<User>
{
}
```

### `[RepositoryPropertyFilter]`

Registers one or more [property filters](./08-filters.md#property-filters) that are
applied to every entity returned by a read operation. Can be specified multiple
times.

```csharp
[RepositoryPropertyFilter(typeof(GeneralUserPropertyFilter))]
public interface IUserRepository : IDatabaseRepository<User>
{
}
```
