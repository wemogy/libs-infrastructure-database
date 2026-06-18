# Soft Delete

Soft delete marks an entity as deleted instead of removing it physically from the
database. Soft-deleted entities are excluded from all read operations, so they
behave as if they were gone, but the data is retained.

## Enabling soft delete

Soft delete is opt-in per repository via the `RepositoryOptions` attribute:

```csharp title="IUserRepository.cs"
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

[RepositoryOptions(enableSoftDelete: true)]
public interface IUserRepository : IDatabaseRepository<User>
{
}
```

The entity needs a `bool` property marked with the `[SoftDeleteFlag]` attribute,
which stores the deletion state. Entities derived from `EntityBase` already provide
an `IsDeleted` property with this attribute:

```csharp
public abstract class EntityBase : IEntityBase
{
    [SoftDeleteFlag]
    public bool IsDeleted { get; set; }

    // ...
}
```

## Behavior

When soft delete is enabled:

- `DeleteAsync` sets the soft-delete flag instead of physically removing the entity.
- All read operations (`GetAsync`, `QueryAsync`, `GetAllAsync`, `IterateAsync`,
  `CountAsync`, ...) automatically exclude soft-deleted entities.
- `GetAsync` throws a `NotFoundErrorException` for an entity that has been
  soft-deleted, exactly as if it did not exist.

```csharp
await userRepository.DeleteAsync(user.Id, user.TenantId);

// throws NotFoundErrorException because the entity is soft-deleted
await userRepository.GetAsync(user.Id, user.TenantId);
```

:::info

When soft delete is **disabled**, `DeleteAsync` removes the entity physically and
read operations return every entity regardless of the soft-delete flag.

:::
