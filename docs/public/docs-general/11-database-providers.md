# Database Providers

The same repository code runs against several database providers. Each provider
ships as its own package and exposes a client factory plus a convenience repository
factory. See [Getting Started](./02-getting-started.md#initializing-a-repository)
for the general initialization flow and dependency-injection setup.

## Azure Cosmos DB

Package: `Wemogy.Infrastructure.Database.Cosmos`

```csharp
var repository = CosmosDatabaseRepositoryFactory.CreateInstance<IUserRepository>(
    "CONNECTION_STRING_HERE",
    "DATABASE_NAME",
    true); // enable insecure development mode for the local emulator
```

Or via dependency injection:

```csharp
var databaseClientFactory = new CosmosDatabaseClientFactory("CONNECTION_STRING_HERE", "DATABASE_NAME");

services
    .AddDatabase(databaseClientFactory)
    .AddRepository<IUserRepository>();
```

Provider specifics:

- Property names are serialized as **camelCase**, and `null` values are omitted.
- The `[ETag]` attribute enables [optimistic concurrency](./09-optimistic-concurrency.md).
- [Transactional batches](./12-transactional-batch.md) are mapped onto the native
  `TransactionalBatch` of the Cosmos SDK.
- [Partial updates](./13-partial-update.md) are mapped onto `PatchItem`, and a patch condition
  onto a filter predicate, which accepts comparisons but no arithmetic on document fields.
- The [change feed](./15-change-feed.md) is mapped onto the change feed processor of the Cosmos SDK.
  It needs a lease container, configured with the `leaseContainerName` argument of the factory and
  defaulting to `leases`, which has to exist with the partition key path `/id`.
- [Multi-tenancy](./04-multi-tenancy.md) is supported.
- The third constructor argument enables *insecure development mode* (gateway
  connection mode and relaxed certificate validation) for use with the local
  Cosmos DB emulator.

## In-Memory

Package: `Wemogy.Infrastructure.Database.InMemory`

The in-memory provider keeps data in process and requires no connection string. It
is meant for unit tests, so the exact same repository code can be exercised without
a real database.

```csharp
var repository = InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>();
```

Provider specifics:

- No external dependency or connection string.
- The store is shared per entity type for the whole process, so every repository over the same
  entity sees the same data regardless of which factory created it. Reset it between tests with
  `DeleteAsync(x => true)`.
- [Multi-tenancy](./04-multi-tenancy.md) is supported.
- [Optimistic concurrency](./09-optimistic-concurrency.md) via the `[ETag]` attribute is supported,
  with the same semantics as Cosmos DB: every write assigns a new eTag, a `ReplaceAsync` with a
  stale eTag fails the precondition, and an `UpsertAsync` carries no precondition.
- Sort keys are compared ordinally, matching Cosmos DB, so the order does not depend on the culture
  of the machine running the tests.
- [Transactional batches](./12-transactional-batch.md) are supported with the same semantics as
  Cosmos DB: every operation is validated before any of them is applied, so a failing batch leaves
  the store untouched.
- [Partial updates](./13-partial-update.md) are supported, applied under the store's lock. A patch
  condition is compiled and evaluated in process, which accepts more than the Cosmos DB filter
  predicate does - a condition doing arithmetic on document fields passes here and is refused
  there.
- The [change feed](./15-change-feed.md) replays the writes of the store in order, with the same
  ordering, checkpointing and redelivery semantics. One logical partition stands in for one physical
  partition key range, and lease contention between two processors sharing a name is not modelled.

:::tip Testing strategy

Reference the in-memory package in your test project and Cosmos DB in your
application. Because both implement the same `IDatabaseClient`, your repository
logic and tests stay provider-independent.

:::

## Feature support matrix

| Feature                                                      | Cosmos DB | In-Memory |
| ----------------------------------------------------------- | :-------: | :-------: |
| CRUD, querying, [sorting & pagination](./05-sorting-pagination.md) | ✅        | ✅        |
| [Soft delete](./07-soft-delete.md)                           | ✅        | ✅        |
| [Read & property filters](./08-filters.md)                   | ✅        | ✅        |
| [Multi-tenancy](./04-multi-tenancy.md)                       | ✅        | ✅        |
| [Optimistic concurrency (ETag)](./09-optimistic-concurrency.md) | ✅        | ✅        |
| [Transactional batch](./12-transactional-batch.md)            | ✅        | ✅        |
| [Partial update (Patch)](./13-partial-update.md)              | ✅        | ✅        |
| [Change feed (latest version)](./15-change-feed.md)          | ✅        | ✅        |
| [Change feed (all versions and deletes)](./15-change-feed.md#all-versions-and-deletes) | ✅ ¹      | ✅        |

¹ Needs a container configured with a full fidelity retention window, and is not served by the
Linux vNext emulator.
