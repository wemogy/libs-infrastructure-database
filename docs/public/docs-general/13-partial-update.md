# Partial Update (Patch)

A partial update changes individual fields of a document without reading it and without sending
the rest of it back. Given a condition, it becomes something the abstraction could not express at
all before: an **atomic check-and-set**.

```csharp
// the balance only moves if it stays within the cap - no read, no retry, no lost update
var updated = await userRepository.PatchAsync(
    user.Id,
    user.TenantId,
    p => p.Increment(x => x.Balance, 1),
    condition: x => x.Balance < x.HardCap);
```

The condition and the operations are **one atomic act on the document**. Two callers racing to
increment the same balance can never both pass a cap they jointly exceed, and a condition that
does not hold is a *business answer* - "denied by the cap" - not a concurrency conflict to retry.

This feature is implemented for the **Azure Cosmos DB** and the **in-memory** provider with the
same semantics and the same errors, so a quota rule can be covered by unit tests.

## Operations

| Operation | Behaviour |
| --- | --- |
| `Set(x => x.Field, value)` | Writes a value, creating the field if the document does not carry it |
| `Increment(x => x.Counter, 5)` | Adds to a `long` field; a field that is not there starts at zero |
| `Increment(x => x.Score, 0.5)` | Adds to a `double` field |

`Increment` takes signed values, so a decrement is `Increment(..., -n)` - there is no
`Decrement`. Operations chain, and up to ten of them are applied in one act:

```csharp
await userRepository.PatchAsync(
    user.Id,
    user.TenantId,
    p => p
        .Set(x => x.Firstname, "Patched")
        .Increment(x => x.LoginCount, 1));
```

## When to prefer it over UpdateAsync

`UpdateAsync` reads the document, applies your change and writes the whole document back. That is
the right tool when the new state depends on the old one in a way only C# can express. A patch is
the right tool when it does not:

- **A counter or a flag.** `Increment` never loses a concurrent update, because it is applied by
  the database, not computed by a caller.
- **One field of a large document.** Only the operations travel, not the document.
- **A rule that has to hold at the moment of the write.** That is the conditional form below, and
  `UpdateAsync` cannot express it - between its read and its write, another caller fits.

## The conditional form

The condition is an ordinary predicate over the entity:

```csharp
condition: x => x.Status == UserStatus.Active && x.Credits < x.CreditsLimit
```

It is evaluated by the database at the moment of the write. If it does not hold, **no operation of
the patch is applied** and a `ConflictErrorException` with the code `PatchConditionNotMet` is
thrown. Inside a [transactional batch](./12-transactional-batch.md), the whole batch is rolled
back.

A missing document is always a `NotFoundErrorException`, with or without a condition - "the
document is not there" and "the state does not permit this" are different answers.

:::caution A condition compares, it does not compute

Cosmos DB translates the condition into a filter predicate, which is parsed by a stricter parser
than a query: it compares fields against constants and against each other, but it does **not**
evaluate arithmetic on document fields. `x => x.Balance + 1 <= x.HardCap` is refused with
`PatchConditionNotSupported`; write the arithmetic on the constant side instead, e.g.
`x => x.Balance < x.HardCap` or `x => x.Balance <= cap - delta` with `cap` and `delta` being
values from your code.

The in-memory provider compiles conditions in process and therefore accepts more than Cosmos DB
does. A condition that passes in a unit test can still be refused against a real database, which
is why the Cosmos test suite covers this case explicitly.

:::

## Why a failed condition is a Conflict and not a PreconditionFailed

Cosmos DB answers a failed conditional patch with HTTP 412, so `PreconditionFailedErrorException`
looks like the obvious mapping. It is the wrong one.

Every repository is wrapped by a retry policy that retries exactly that exception type three
times with an exponential backoff, because a stale eTag means *"someone changed this, read again
and decide"* - which a retry can resolve. A failed patch condition means *"the state does not
permit this"*, and it is deterministic: the same call against the same state fails identically.
Mapping it to `PreconditionFailed` would burn three retries and a backoff before returning an
answer the first attempt already had.

So the two stay apart, and a caller can tell them apart even when one batch carries both:

| Cause | Exception | Code |
| --- | --- | --- |
| Condition did not hold | `ConflictErrorException` | `PatchConditionNotMet` |
| Stale eTag on a `Replace` | `PreconditionFailedErrorException` | `EtagMismatch` |

## Inside a transactional batch

A patch is an operation of a [transactional batch](./12-transactional-batch.md), so a conditional
increment and a document create commit together or not at all:

```csharp
await userRepository
    .CreateTransactionalBatch(tenantId)
    .Create(usageEvent)
    .Patch(
        account.Id,
        p => p.Increment(x => x.Credits, 1),
        condition: x => x.Credits < x.CreditsLimit)
    .ExecuteAsync();
```

`PatchAsync` returns the patched document - a single patch has exactly one result, and the new
value is usually the reason for the call. `ExecuteAsync` on a batch returns nothing, because a
per-item result would need an index-aligned type its other operations cannot fill. The single
patch therefore asks for the write response and pays its request charge; the batch does not.

## Errors

| Situation | Exception | Code |
| --- | --- | --- |
| Condition did not hold | `ConflictErrorException` | `PatchConditionNotMet` |
| Document does not exist | `NotFoundErrorException` | `EntityNotFound` |
| Path is not a chain of member accesses, or is not writable | `UnexpectedErrorException` | `PatchPathNotSupported` |
| Path targets the id, the partition key or the eTag | `UnexpectedErrorException` | `PatchPathNotAllowed` |
| More than ten operations | `UnexpectedErrorException` | `PatchOperationLimitExceeded` |
| No operations | `UnexpectedErrorException` | `PatchIsEmpty` |
| Condition uses an unsupported construct | `UnexpectedErrorException` | `PatchConditionNotSupported` |

The path errors are thrown while the operations are collected, before any I/O.

## Constraints

- **One document.** A patch addresses one id in one partition.
- **Ten operations.** The Cosmos DB limit, enforced for every provider.
- **An empty patch is an error**, not a no-op - unlike an empty batch, which a caller can reach by
  looping over an empty collection, a patch without operations is always a mistake at the call
  site.
- **The id, the partition key and the eTag cannot be patched.** Changing an id or a partition key
  relocates a document, and the eTag belongs to the provider. Both are rejected before any I/O.
- **Paths are member accesses.** `x => x.Balance` and `x => x.Inner.Value` are paths;
  a method call, an indexer or a computed member is not. The field name is resolved through the
  serializer, so a `[JsonProperty]` override is honoured.
- **Filters do not apply.** Read filters, property filters and soft delete are not applied to a
  patch, consistent with the other write paths.

## Not supported

- **`Add`, `Remove`, `Move` and array-index paths.** `Set` and `Increment` cover the cases this
  exists for.
- **`decimal` increments.** Cosmos DB increments a field as a 64-bit integer or as a double.
  Narrowing a `decimal` to a `double` would silently lose precision on values that are usually
  money, so there is no overload that does it. Keep such a value in a `long` of minor units, or
  read-modify-write it with `UpdateAsync`.
- **Patching across documents or partitions.**
