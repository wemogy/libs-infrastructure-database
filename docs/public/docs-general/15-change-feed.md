# Change Feed

The change feed is the ordered log of the writes a collection received. Reading it is how a
projection, a materialized view or an outbound notification reacts to writes without polling
the collection and without leaving the repository abstraction.

This feature is implemented for the **Azure Cosmos DB** and the **in-memory** provider with the
same semantics, so the projection built on top of it can be covered by unit tests against the
in-memory provider.

## Reading the feed

Create a processor from the repository, start it, and stop it when the application shuts down:

```csharp
await using var processor = userRepository.CreateChangeFeedProcessor(
    "billing-projection",
    async (changes, context, cancellationToken) =>
    {
        foreach (var user in changes)
        {
            await projection.ApplyAsync(user, cancellationToken);
        }
    });

await processor.StartAsync();
```

Nothing is read until `StartAsync` is awaited. `StopAsync` — or disposing the processor, which
stops it — releases the leases so another instance can pick them up without waiting for them to
expire.

The handler is only ever invoked with a **non-empty** batch, and the batch is only checkpointed
once the returned task completed. A handler that throws leaves its batch uncheckpointed, so the
same changes are read again on the next poll: the delivery is **at least once**, never exactly
once. Make the handler idempotent, or accept that a projection can see a change twice.

## Two properties worth knowing before you build on it

### Ordering is per range, not per partition key

`context.RangeId` identifies the **physical partition key range** the batch was read from. Changes
carrying the same range id arrive in the order they were written; changes from two different range
ids have no order relative to each other, and there is no global order at all.

A physical range holds many logical partition keys, and it **splits as the data in it grows** —
the id of a range that split is replaced by the ids of the ranges it split into. So:

- ordering **within one logical partition key** holds, because a logical partition always lives in
  exactly one physical range;
- ordering **between two logical partition keys** does not, even if they happen to share a range
  today;
- `RangeId` is the scope of the ordering guarantee, not a key to persist. A cursor built on it
  breaks the first time the container splits.

### A patch arrives as the whole document

A [partial update](./13-partial-update.md) writes only the fields its operations address, but the
change it produces carries the **entire document**, exactly like a replace does. A projection can
therefore rebuild its state from a change without knowing which write produced it, and without
having to merge a partial payload into what it already had.

## What the latest version feed does and does not carry

`CreateChangeFeedProcessor` reads the *latest version* feed. It carries the document **as it is
now**, not as each write left it:

| Situation | On the feed |
| --- | --- |
| A document created and then replaced between two reads | Once, carrying the replace |
| A document patched | Once, carrying the whole document |
| A document hard-deleted | **Not at all** |
| A document [soft-deleted](./07-soft-delete.md) | Yes — a soft delete is a write like any other |
| A document created and then hard-deleted between two reads | Not at all |

Hard deletes being invisible is the usual surprise. A projection that has to react to them either
soft-deletes instead, or reads the all-versions-and-deletes feed below.

The feed is also **unfiltered**: [read filters](./08-filters.md) and the soft delete filter shape
what a query returns, and they are deliberately not applied here. A document a filter hides is
still a document that changed.

## Where a processor starts reading

| State | Starts at |
| --- | --- |
| No checkpoint, default options | The current end of the feed — only writes made after `StartAsync` |
| No checkpoint, `StartFromBeginning = true` | The beginning of the container |
| A checkpoint exists | Where the previous processor of that name stopped |

A checkpoint always wins over the option, so restarting a processor never replays what it already
handled, and never skips what was written while it was down.

`StartFromBeginning` is for building a projection from the documents that already exist without a
separate backfill. On a container that is already large, that is a lot of reading — which is why it
is off by default.

## The processor name

The name is what the leases and the checkpoint are filed under:

- **Several instances under one name** split the ranges between them, so a deployment scaled to
  three pods processes each change once rather than three times. This is where "exactly one
  processor per deployment" stops having to be arranged out of band.
- **A different name** reads the same feed independently, from its own position. Two projections
  over one collection want two names.

Each instance also needs an `InstanceName` unique among the instances sharing the processor name.
It defaults to the machine name, which is unique per pod, container or VM in the usual deployment.

## All versions and deletes

`CreateAllVersionsAndDeletesChangeFeedProcessor` reads every write separately rather than the
current state of what changed — which is what an event log wants and a state projection does not:

```csharp
await using var processor = userRepository.CreateAllVersionsAndDeletesChangeFeedProcessor(
    "audit-log",
    async (changes, context, cancellationToken) =>
    {
        foreach (var change in changes)
        {
            switch (change.Operation)
            {
                case DatabaseChangeOperation.Create:
                    await auditLog.RecordCreateAsync(change.Current!);
                    break;
                case DatabaseChangeOperation.Replace:
                    await auditLog.RecordUpdateAsync(change.Previous, change.Current!);
                    break;
                case DatabaseChangeOperation.Delete:
                    // the removed document is only still available as the previous version
                    await auditLog.RecordDeleteAsync(change.Previous!);
                    break;
            }
        }
    });
```

| Property | Meaning |
| --- | --- |
| `Operation` | `Create`, `Replace` or `Delete` |
| `Current` | The document after the write; `null` for a delete |
| `Previous` | The document before the write; `null` for a create |
| `IsTimeToLiveExpired` | Whether a delete was the time to live expiring rather than an explicit delete |

Three constraints come with it:

- **`StartFromBeginning` is refused.** The previous versions and the deletes only exist inside the
  retention window of the container, so there is no beginning to read from. Asking for it throws
  `ChangeFeedStartFromBeginningNotSupported`.
- **Cosmos DB needs the container configured for it.** The container has to carry a full fidelity
  retention window (`ChangeFeedPolicy.FullFidelityRetention`). A container without one delivers
  **no changes at all** rather than failing, which is a quiet way to lose an audit log — check the
  container before you rely on it.
- **`Previous` is only meaningful on a container that retains previous versions.** Cosmos DB sends
  an empty object rather than nothing for a version it does not carry, and an entity that fills in
  its own id — as `EntityBase` does — is indistinguishable from a real document once deserialized.
  The provider normalizes the version the operation rules out, so a create never carries a previous
  version and a delete never a current one; what it cannot do is tell an unretained previous version
  of a *replace* from a real one. Since the feed only delivers anything at all on a container with a
  retention window, a replace that reaches your handler has one.

## The lease container (Cosmos DB)

The Cosmos DB provider keeps the leases and the checkpoints in a lease container, configured once
on the client factory:

```csharp
var databaseClientFactory = new CosmosDatabaseClientFactory(
    connectionString,
    databaseName,
    leaseContainerName: "leases"); // the default
```

One container serves every processor of the database — a lease is filed under the name of the
processor that owns it, so processors of different collections do not collide.

It has to **exist**, with the partition key path `/id`, before a processor is started. The provider
deliberately does not create it: creating a container means deciding its throughput, which is not a
decision a library should make on an account's behalf. Starting a processor without it throws
`ChangeFeedContainerNotFound`.

## Errors and failures

Pass `OnError` to learn about failures instead of watching a handler retry in silence:

```csharp
var options = new ChangeFeedProcessorOptions
{
    OnError = (context, exception) =>
    {
        logger.LogError(exception, "Change feed failed on range {RangeId}", context.RangeId);
        return Task.CompletedTask;
    }
};
```

The processor keeps running after a failure — the uncheckpointed batch is simply read again — so
without `OnError` a handler that keeps throwing looks exactly like a feed with nothing on it.

| Situation | Exception | Code |
| --- | --- | --- |
| The processor name is empty | `UnexpectedErrorException` | `ChangeFeedProcessorNameIsEmpty` |
| `StartFromBeginning` on the all-versions feed | `UnexpectedErrorException` | `ChangeFeedStartFromBeginningNotSupported` |
| `StartAsync` on a running processor | `UnexpectedErrorException` | `ChangeFeedProcessorAlreadyStarted` |
| The monitored or the lease container is missing | `UnexpectedErrorException` | `ChangeFeedContainerNotFound` |

## Multi-tenancy

A change feed on a [multi-tenant](./04-multi-tenancy.md) repository is filtered to the current
tenant, and the partition key values the handler sees carry no tenant prefix — the same way every
other operation behaves there.

The processor name is prefixed with the tenant as well. Without that, the processors of two tenants
would share one set of leases: they would split the ranges of the container between them and each
would then drop everything the other tenant's ranges carried, so every tenant would silently see
only part of its own changes. The cost is one lease set per tenant, and every tenant reads the whole
feed and keeps only its own share of it.

## Testing against the in-memory provider

The in-memory provider replays its writes in order with the same semantics, so a projection can be
tested without a database. Two differences are worth knowing:

- **One logical partition is one range.** `RangeId` is the partition key value, with the components
  of a [hierarchical key](./14-hierarchical-partition-keys.md) joined by `/`. That is narrower than a
  physical range of a real container, and never promises an order Cosmos DB would not keep — a test
  that passes here does not pass because of a guarantee production does not have.
- **Lease contention is not modelled.** Two processors running under the same name each see every
  change instead of splitting the ranges between them. Checkpointing *is* modelled, so a test can
  stop a processor, write, start it again under the same name and assert it caught up.
- **The change log is kept for whatever could still be read.** Nothing is recorded until the first
  processor starts, and once a checkpoint exists the writes after it are retained for the lifetime of
  the process — a stopped processor is entitled to resume from its checkpoint, so those writes cannot
  be dropped. That is more conservative than Cosmos DB, which retains the feed for the retention
  window of the container whether or not a lease exists.

:::caution The Cosmos DB emulator

The Linux vNext emulator does not serve the all-versions-and-deletes feed: it accepts a container
with a full fidelity retention window, reports the window back as `00:00:00`, and delivers nothing.
It also replays a container from the beginning regardless of the start position asked for. Both
differ from a deployed account — cover those two behaviours against the in-memory provider, or
against a real database.

:::
