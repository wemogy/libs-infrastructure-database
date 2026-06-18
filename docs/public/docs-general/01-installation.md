# Installation

The library is split into a provider-independent core package and one package per
database provider. Install the core package together with the provider you want to
use.

| Package                                  | Description                                         |
| ---------------------------------------- | --------------------------------------------------- |
| `Wemogy.Infrastructure.Database.Core`    | Provider-independent abstractions (entities, repositories, attributes, filters). Referenced transitively by every provider package. |
| `Wemogy.Infrastructure.Database.Cosmos`  | Azure Cosmos DB implementation.                     |
| `Wemogy.Infrastructure.Database.Mongo`   | MongoDB implementation.                             |
| `Wemogy.Infrastructure.Database.InMemory`| In-memory implementation, primarily for unit tests. |

## Supported frameworks

The packages target **.NET 8**, **.NET 9** and **.NET 10**.

:::caution Breaking change

Starting with the next major version, the libraries no longer target frameworks
older than .NET 8. Upgrade your consuming projects to at least `net8.0` before
updating.

:::

## Adding a provider package

Add the provider package you need; it pulls in the core package automatically.

```bash title="Azure Cosmos DB"
dotnet add package Wemogy.Infrastructure.Database.Cosmos
```

```bash title="MongoDB"
dotnet add package Wemogy.Infrastructure.Database.Mongo
```

```bash title="In-Memory (unit tests)"
dotnet add package Wemogy.Infrastructure.Database.InMemory
```

A common setup references the in-memory package in your test project and the real
provider (Cosmos or Mongo) in your application project, so the exact same
repository code can be exercised against an in-memory database during tests. See
[Database Providers](./11-database-providers.md) for provider-specific details.
