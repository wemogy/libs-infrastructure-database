# Transactional Batch

A transactional batch applies several write operations **atomically**: either every
operation of the batch succeeds, or none of them is applied. It solves the *dual write*
problem — writing two documents that only make sense together, for example an
append-only event and the aggregate document it advances. Without a batch, a failure
between the two writes leaves the store in a state that no reader can make sense of.

This feature is implemented for the **Azure Cosmos DB** and the **in-memory** provider
with the same semantics and the same errors, so the atomicity of your code can be
covered by unit tests against the in-memory provider.

## Using a batch

Start a batch from the repository, add operations, execute it:

```csharp
await userRepository
    .CreateTransactionalBatch(tenantId)
    .Create(newUser)
    .Replace(existingUser)
    .Delete(obsoleteUserId)
    .ExecuteAsync();
```

Every method returns the batch, so calls chain. `Create`, `Replace`, `Upsert` and
`Delete` mirror their repository counterparts:

| Operation | Behaviour |
| --- | --- |
| `Create(entity)` | Fails the batch if the id already exists |
| `Replace(entity)` | Fails the batch if the entity does not exist, or if its eTag is stale |
| `Upsert(entity)` | Inserts or updates, no precondition |
| `Delete(id)` | Hard delete, fails the batch if the entity does not exist |

## How it works

The argument of `CreateTransactionalBatch` is the **logical partition** the batch runs
against. Every entity added to it has to live in that partition: `Create`, `Replace`
and `Upsert` throw immediately — when the operation is *added*, not when the batch is
executed — if the `[PartitionKey]` value of the entity differs, so the stack trace
points at the offending call.

For an entity with a [hierarchical partition key](./14-hierarchical-partition-keys.md), the
partition is the **whole** hierarchy: every component has to match, not just the broadest one.

The Cosmos DB provider maps the batch onto the native `TransactionalBatch` of the
Cosmos SDK, which the service applies as one atomic unit. When one operation fails,
Cosmos rejects the whole batch and reports every other operation as
`424 FailedDependency`; the provider finds the operation that actually failed and
translates it into the same exception the equivalent single write would throw, with the
index of the failing operation in the message.

The in-memory provider validates *all* operations against a working copy of the
partition before it applies *any* of them, so a failing batch cannot leave a partial
write behind. Operations are validated in order against the state at execution time,
which means a `Create` followed by a `Replace` of the same id **inside one batch** is
valid — the replace sees what the create added.

## Errors

| Situation | Exception | Code |
| --- | --- | --- |
| Entity's partition key ≠ batch partition key | `UnexpectedErrorException` | `PartitionKeyMismatch` |
| More than 100 operations | `UnexpectedErrorException` | `TransactionalBatchOperationLimitExceeded` |
| `Create` on an existing id | `ConflictErrorException` | `AlreadyExists` |
| `Replace` / `Delete` on a missing entity | `NotFoundErrorException` | `EntityNotFound` |
| Stale eTag on `Replace` | `PreconditionFailedErrorException` | `EtagMismatch` |
| Any other batch failure | `FailureErrorException` | `TransactionalBatchFailed` |
| Executing a batch twice, or adding to an executed batch | `UnexpectedErrorException` | `TransactionalBatchAlreadyExecuted` |

Optimistic concurrency carries over: a `Replace` of an entity that opts into
[eTags](./09-optimistic-concurrency.md) sends the eTag it was read with as a
precondition, and a mismatch fails the **whole** batch.

## Constraints

- **One logical partition.** A batch cannot span partitions or containers. That is a
  Cosmos DB constraint and it stays a constraint here.
- **One entity type.** A batch is created from one repository and only accepts that
  repository's entity type. To write several types into one partition, use a
  [partition batch](#mixed-type-partition-batch).
- **100 operations.** Adding the 101st operation throws, on every provider, so a batch
  that passes its test against the in-memory provider is not too large for Cosmos DB.
- **An empty batch is a no-op.** `ExecuteAsync` on a batch without operations returns
  without a round trip and does not throw, so a batch built in a loop needs no guard.
- **Single-use and not thread-safe.** Build a batch from one thread and execute it once. A
  second `ExecuteAsync`, or an operation added after the execution, throws instead of replaying
  the writes.

:::caution No entities are returned, and no retry happens

`ExecuteAsync` returns `Task`, not the written entities. A per-item result would need an
index-aligned type that `Delete` cannot fill, and skipping the write response keeps the
request charge down. A caller that needs the state after the write — the new eTag, for
example — re-reads the entity.

The retry policy that recovers `UpdateAsync` from an eTag conflict wraps the
*repository*, not the batch, so a batch is **never retried**. That is intentional: like
a bare `ReplaceAsync` (see [Optimistic Concurrency](./09-optimistic-concurrency.md)), a
batch of stale entities carries the same outdated eTags on every attempt and would fail
identically every time. Re-read the entities, rebuild the batch, execute it again.

:::

## Multi-tenancy

A [multi-tenant](./04-multi-tenancy.md) repository prefixes the partition key of the batch and of
every entity added to it with the tenant id, just like its other write methods do. Pass the
unprefixed partition key, exactly as you would to `GetAsync`; the instance you hand to `Create`,
`Replace` or `Upsert` is not modified, the batch works on a copy of it.

That copy is taken when the operation is *added*, while a plain repository reads the entity when
the batch is *executed*. Mutating an entity after adding it to a batch therefore reaches the store
through a plain repository but not through a multi-tenant one — do not rely on either: set the
entity up before you add it.

## Mixed-type partition batch

The typed batch above writes one entity type. When you need to write documents of
**different** shapes together — the metered-usage case where a consume records a
`UsageEvent` *and* moves a `QuotaBalance`, two types that share one container — start a
**partition batch** instead:

```csharp
await usageEventRepository
    .CreatePartitionBatch(partitionKey)
    .Create(usageEvent)
    .Patch<QuotaBalance>(
        balanceId,
        p => p.Increment(x => x.Consumed, 1),
        x => x.Consumed < cap)
    .ExecuteAsync();
```

`CreatePartitionBatch` returns an **untyped** batch bound to the repository's own
container. Because the type is no longer fixed by the repository, every operation names
its own: `Create<T>`, `Replace<T>`, `Upsert<T>`, `Delete<T>(id)` and `Patch<T>`. The
generic argument is inferred from the entity for `Create`, `Replace` and `Upsert`, and
given explicitly for `Delete<T>` and `Patch<T>`, which carry only an id.

Everything else is the typed batch: it is atomic, limited to **one logical partition**
and **one container** (the repository's), capped at 100 operations, single-use, and it
throws the [same errors](#errors) — a conflicting `Create`, a stale `Replace`, a failed
patch condition all roll the **whole** batch back, across the type boundary. The
`PatchConditionNotMet` of a conditional [patch](./13-partial-update.md) stays distinct
from the `EtagMismatch` of a stale replace, so a caller can tell *"the state does not
permit this"* from *"someone changed this"*.

A [multi-tenant](./04-multi-tenancy.md) repository prefixes the tenant id onto the
partition key of every operation, whatever its type, exactly as it does for the typed
batch.

:::warning One container, several types — and the in-memory provider cannot check it
A partition batch writes into the container of the repository it was created from, and the
library cannot verify that the types you add belong there: a container is configured per
**repository interface** (`[RepositoryOptions(collectionName:)]`), not per entity type, so
`Create<T>` has no container of its own to compare against. Adding a type whose repository
is mapped to a *different* container is therefore not refused — on Cosmos DB the document
lands in **this** batch's container, where its own repository will never find it. Choosing
the repository is how you declare which container the batch writes to; make sure every
type you add is mapped to it.

The in-memory provider will not catch a mistake here. It keeps one store per entity type
and ignores containers entirely, so a batch mixing types from different containers passes
in-memory, and two types sharing an id in one partition succeed in-memory where Cosmos DB
answers `409` → `AlreadyExists`. Unlike the operation cap, this invariant is **not**
covered by a green in-memory test — verify a co-located mapping against Cosmos DB.

Co-locating types in one container also means an ordinary `QueryAsync` over one type sees
the others, since the repository adds no type discriminator — address the documents by id,
or keep the types in separate containers if you query them.
:::

## Not supported

- **Cross-partition or cross-container batches**, which Cosmos DB cannot do either — a
  partition batch stays inside one logical partition of one container.
