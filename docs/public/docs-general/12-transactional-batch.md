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
  repository's entity type.
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

## Not supported

- **Mixed entity types in one batch.** A Cosmos container can hold several types, but
  the repository abstraction resolves id, partition key and eTag per entity type.
- **Patch operations** such as an atomic `Increment`.
- **Cross-partition or cross-container batches**, which Cosmos DB cannot do either.
