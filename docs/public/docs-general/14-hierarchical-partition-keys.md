# Hierarchical Partition Keys

A hierarchical partition key partitions documents by **up to three values** instead of one,
ordered from the broadest to the narrowest. It is the answer to the two ceilings a single-value
key runs into once one key value keeps growing:

- **20 GB per logical partition.** A temporary increase exists, but it voids the SLA.
- **10,000 RU/s per logical partition.** A hot key is throughput-capped as well, and no amount of
  container-level provisioning helps.

With a hierarchy, the store is free to spread one broad value over several physical partitions,
while an operation that names the **whole** key — a point read, a patch, a transactional batch —
still addresses exactly one logical partition.

This feature is implemented for the **Azure Cosmos DB** and the **in-memory** provider with the
same semantics and the same errors, so code that relies on it can be covered by unit tests against
the in-memory provider.

## Declaring the key

Mark every property that forms the key with `[HierarchicalPartitionKey]`, numbering them from the
broadest component to the narrowest:

```csharp title="UsageEvent.cs"
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

public class UsageEvent : EntityBase
{
    [HierarchicalPartitionKey(0)]
    public string CustomerId { get; set; } = string.Empty;

    [HierarchicalPartitionKey(1)]
    public string MeterSlug { get; set; } = string.Empty;

    [HierarchicalPartitionKey(2)]
    public string TimeBucket { get; set; } = string.Empty;

    public long Quantity { get; set; }
}
```

An entity declares its partition key **either** with `[HierarchicalPartitionKey]` **or** with
`[PartitionKey]`, never with both. Entities that already use `[PartitionKey]` need no change.

The declaration is validated when the repository is built, so a mistake fails at startup rather
than on the first write:

| Error code                      | Cause                                                             |
| ------------------------------- | ----------------------------------------------------------------- |
| `PartitionKeyDefinitionAmbiguous` | Both attributes used, `[PartitionKey]` on more than one property, or the orders are not contiguous from 0 |
| `PartitionKeyValueTooDeep`      | More than three components declared                                |
| `PartitionKeyPropertyNotAString` | A partition key property is not a `string`                        |
| `PartitionKeyPropertyNotFound`  | No partition key declared at all                                   |

:::caution The order has to match the container

The numbering maps onto the partition key paths the container was created with. A container
created with `/customerId`, `/meterSlug`, `/timeBucket` requires exactly that order in the
attributes. The library does not create containers, so this is not checked for you.

:::

## Addressing a document

Every method that takes a partition key accepts a `PartitionKeyValue`. A single value converts
implicitly, so nothing changes for entities with a `[PartitionKey]`:

```csharp
// unchanged
await userRepository.GetAsync(user.Id, user.TenantId);
```

For a hierarchy, build the key from its components:

```csharp
using Wemogy.Infrastructure.Database.Core.ValueObjects;

var partitionKey = new PartitionKeyValue(customerId, meterSlug, timeBucket);

var usageEvent = await usageEventRepository.GetAsync(id, partitionKey);
await usageEventRepository.PatchAsync(id, partitionKey, p => p.Increment(x => x.Quantity, 1));
await usageEventRepository.DeleteAsync(id, partitionKey);
```

The **whole** key addresses the document. A key that matches on the broader components but differs
in a narrower one is a different logical partition, and a read against it does not find the
document.

## Transactional batches

A batch is limited to one logical partition, which for a hierarchical key means the **entire**
hierarchy. This is the case the feature exists for — a balance and the event that advances it,
written atomically, while the store stays free to split the customer's tail:

```csharp
var partitionKey = new PartitionKeyValue(customerId, meterSlug, timeBucket);

var batch = usageEventRepository.CreateTransactionalBatch(partitionKey);
batch.Create(usageEvent);
batch.Patch(balanceId, p => p.Increment(x => x.Quantity, usageEvent.Quantity));
await batch.ExecuteAsync();
```

Adding an entity whose key differs in any component throws `PartitionKeyMismatch` when the
operation is added.

## Queries

Queries are unaffected: filter on the key properties like any other property.

```csharp
// every leaf of one customer
var usageEvents = await usageEventRepository.QueryAsync(x => x.CustomerId == customerId);
```

:::note

A query scoped to a *prefix* of the key is currently served as a cross-partition query with a
filter — correct, but not as cheap as it could be. Cosmos DB can restrict such a query to the
matching physical partitions; that optimization is tracked in
[issue #165](https://github.com/wemogy/libs-infrastructure-database/issues/165).

:::

## Multi-tenancy

The [multi-tenant plugin](./04-multi-tenancy.md) composes its tenant prefix into the **broadest
component only**, leaving the narrower components as the entity set them. Tenant isolation still
rests on a `StartsWith` guard over that component, so the secure-by-default property is unchanged.

## Patching

A component of a hierarchical key cannot be patched, exactly as a single-value partition key
cannot: moving a document to another partition is a delete and a create, not an update. Such a
path is rejected with `PatchPathNotAllowed`.
