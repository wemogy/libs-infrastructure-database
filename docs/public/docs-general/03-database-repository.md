# Database Repository

The `IDatabaseRepository<TEntity>` interface exposes the operations below. All
methods are asynchronous and respect the configured [read & property
filters](./08-filters.md) and [soft delete](./07-soft-delete.md) behavior.

## CreateAsync

The `CreateAsync` method is used to insert an entity in the database. It throws a ```ConflictErrorException``` if an entity with the same id already exists.

## GetAsync

The `GetAsync` methods provide several ways to get a single entity from the database (by id, by id and partition key, or by a predicate).
A ```NotFoundErrorException``` exception is thrown if the entity does not exist or if it has been soft-deleted.

:::tip

Prefer the overload that takes both `id` and `partitionKey`. Resolving an entity by id alone is significantly less efficient because it cannot target a single partition.

:::

## GetAllAsync

The `GetAllAsync` method returns all entities of the repository (respecting read and property filters as well as soft delete).

## GetByIdsAsync

The `GetByIdsAsync` method returns all entities whose id is contained in the given list of ids.

## QueryAsync

The `QueryAsync` methods provide several ways to get multiple entities from the database, either via a predicate or via `QueryParameters`. They support [sorting & pagination](./05-sorting-pagination.md).

## QuerySingleAsync

The `QuerySingleAsync` method provides a way to get a single entity from the database. It throws a ```PreconditionFailedErrorException``` if more results are returned than the expected one. It also throws a ```NotFoundErrorException``` when no result is found.

## CountAsync

The `CountAsync` method provides a ways to count entities from the database, which match a given filter.

## ExistsAsync

The `ExistsAsync` methods are used to check if entities exist in the database. They all return true when found or false otherwise.

## EnsureExistsAsync

The `EnsureExistsAsync` methods are used to check if entities exist in the database. They throw a ```NotFoundErrorException``` if the no entities are found.

## IterateAsync

The `IterateAsync` methods are used to iterate the repository via a filter and apply an operation on the filtered results.

## ReplaceAsync

The `ReplaceAsync` method can be used to replace an existing entity in the database. When the entity opts into [optimistic concurrency](./09-optimistic-concurrency.md) via the `[ETag]` attribute, a stale replace throws a ```PreconditionFailedErrorException```.

## UpsertAsync

The `UpsertAsync` methods insert the entity if it does not exist yet, or replace it otherwise. Upserts are unconditional and do not perform an eTag check.

## UpdateAsync

The `UpdateAsync` methods read an entity, apply a given update action (synchronous or asynchronous) and replace it. For entities using [optimistic concurrency](./09-optimistic-concurrency.md), a concurrent modification is retried automatically (read-modify-write with a fresh eTag) before the ```PreconditionFailedErrorException``` is surfaced.

## DeleteAsync

The `DeleteAsync` methods remove an entity by id, by id and partition key, or by a predicate. When [soft delete](./07-soft-delete.md) is enabled, the entity is flagged as deleted instead of being physically removed. Prefer the overload with a partition key (or include the partition key in the predicate) for best performance.
