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
| `[SoftDeleteFlag]` | Property | No       | Marks the `bool` property that flags an entity as soft-deleted (see [Soft Delete](./07-soft-delete.md)). |
| `[ETag]`           | Property | No       | Opts the entity into optimistic concurrency (see [Optimistic Concurrency](./09-optimistic-concurrency.md)). |

When you derive from `EntityBase`, `[Id]` and `[SoftDeleteFlag]` are already
provided. `GlobalEntityBase` additionally provides a global `[PartitionKey]`.

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
