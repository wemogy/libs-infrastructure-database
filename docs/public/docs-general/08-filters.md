# Read & Property Filters

Filters let you enforce cross-cutting rules on a repository without repeating them
at every call site. They are registered on the repository interface via attributes
and resolved from the dependency injection container, so they can depend on other
services (for example the current user or tenant).

There are two kinds of filters:

- **Read filters** restrict *which* entities are visible.
- **Property filters** modify *which data* of the returned entities is exposed.

## Read filters

A read filter contributes a predicate that is combined (logical AND) with every
read operation. Entities that do not match are invisible to the repository: they
are excluded from `QueryAsync`, `GetAllAsync`, `IterateAsync` and `CountAsync`, and
`GetAsync` throws a `NotFoundErrorException` for them.

Implement `IDatabaseRepositoryReadFilter<TEntity>`:

```csharp title="GeneralUserReadFilter.cs"
using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

public class GeneralUserReadFilter : IDatabaseRepositoryReadFilter<User>
{
    public Task<Expression<Func<User, bool>>> FilterAsync()
    {
        return Task.FromResult((Expression<Func<User, bool>>)(x => x.Firstname != "John"));
    }
}
```

Register it with the `RepositoryReadFilter` attribute (multiple filters are allowed
and all of them must match):

```csharp title="IUserRepository.cs"
[RepositoryReadFilter(typeof(GeneralUserReadFilter))]
public interface IUserRepository : IDatabaseRepository<User>
{
}
```

:::tip Use case

Read filters are a robust way to implement row-level security (for example
"only return entities owned by the current user"), because the rule is applied
centrally and cannot be forgotten at a call site.

:::

## Property filters

A property filter runs over the entities returned by a read operation and can
mutate them, for example to strip sensitive fields. It does not change what is
stored in the database, only what the caller receives.

Implement `IDatabaseRepositoryPropertyFilter<TEntity>`:

```csharp title="GeneralUserPropertyFilter.cs"
using System.Collections.Generic;
using System.Threading.Tasks;
using Wemogy.Infrastructure.Database.Core.Abstractions;

public class GeneralUserPropertyFilter : IDatabaseRepositoryPropertyFilter<User>
{
    public Task FilterAsync(List<User> entities)
    {
        foreach (var entity in entities)
        {
            entity.PrivateNote = string.Empty;
        }

        return Task.CompletedTask;
    }
}
```

Register it with the `RepositoryPropertyFilter` attribute (multiple filters are
allowed):

```csharp title="IUserRepository.cs"
[RepositoryPropertyFilter(typeof(GeneralUserPropertyFilter))]
public interface IUserRepository : IDatabaseRepository<User>
{
}
```

The filter is applied to all read operations, so the `PrivateNote` of every user
returned by `GetAsync`, `QueryAsync`, `GetAllAsync` or `IterateAsync` is cleared.
