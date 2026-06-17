# Error Handling

Repository methods translate provider-specific failures into a small set of typed
exceptions from `Wemogy.Core.Errors.Exceptions`. This keeps error handling
provider-independent: the same `catch` works whether the repository is backed by
Cosmos DB, MongoDB or the in-memory implementation.

| Exception                           | Raised when                                                                                   |
| ----------------------------------- | --------------------------------------------------------------------------------------------- |
| `NotFoundErrorException`            | A requested entity does not exist (or is soft-deleted / hidden by a [read filter](./08-filters.md)). Thrown by `GetAsync`, `QuerySingleAsync`, `EnsureExistsAsync` and `DeleteAsync`. |
| `ConflictErrorException`            | `CreateAsync` is called with an id that already exists.                                        |
| `PreconditionFailedErrorException`  | An [optimistic concurrency](./09-optimistic-concurrency.md) check failed (eTag mismatch), or `QuerySingleAsync` matched more than one entity. |
| `UnexpectedErrorException`          | An entity is misconfigured (for example a missing `[Id]` or `[PartitionKey]` attribute), or another unexpected error occurred. |

## Example

```csharp
using Wemogy.Core.Errors.Exceptions;

try
{
    var user = await userRepository.GetAsync(id, partitionKey);
}
catch (NotFoundErrorException)
{
    // the entity does not exist, was soft-deleted, or is hidden by a read filter
}
```

Each exception carries a stable error `Code` (for example `EntityNotFound`,
`AlreadyExists`, `EtagMismatch`) and a human-readable message. The not-found message
also includes the entity type name and partition key to make debugging easier.

:::info

`ExistsAsync` does not throw for a missing entity; it returns `false`. Use
`EnsureExistsAsync` when you want a `NotFoundErrorException` instead.

:::
