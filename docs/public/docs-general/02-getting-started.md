# Getting started

## Creating an entity

All entity classes **must** implement the `IEntityBase` interface. Per default, all ids must be converted to type ```string```.

### Partition Key

Each entity should define a **partition key** property, which identifies which entities should be stored closely together to improve the read performance.

The **partition key** property is indicated by the `PartitionKey` attribute:

```csharp title="Partition Key sample"
[PartitionKey]
public Guid TenantId { get; set; }
```

In case that you can't identify a **partition key** property, because all your entities are global, it's mandatory to define a dummy partition key property, which has always the same value. We already provide a solution for that:

```csharp title="Global Partition Key sample"
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Constants;

[PartitionKey]
public string PartitionKey { get; set; } = PartitionKeyDefaults.GlobalPartition;
```

:::info Pro Tip

Use the `GlobalEntityBase` class as base class to get the default **partition key** included.

:::

### Sample entity implementation

```csharp title="User.cs"
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

public class User : EntityBase
{
  [PartitionKey]
  public Guid TenantId { get; set; }

  public string Firstname { get; set; }

  public User()
  {
    TenantId = Guid.Empty;
    Firstname = string.Empty;
  }
}
```

## Declaring the Repository Interface

```csharp title="IUserRepository.cs"
using Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IUserRepository : IDatabaseRepository<User>
{
}
```

### Repository options

Using the `RepositoryOptions` attribute you can customize the following options for a repository:

| Option             | Description                                                   | Default                                                                                                 |
| ------------------ | ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------- |
| `enableSoftDelete` | Controls, if the repository should use soft-delete by default | `true` if entity implements `ISoftDeletable`<br />`false` if entity **not** implements `ISoftDeletable` |

```csharp title="IUserRepository.cs"
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;

[RepositoryOptions(enableSoftDelete: true)]
public interface IUserRepository : IDatabaseRepository<User>
{
}
```

## Initializing a Repository

### DatabaseRepositoryFactory

```csharp
// create database client factory
IDatabaseClientFactory databaseClientFactory = new CosmosDatabaseClientFactory("CONNECTION_STRING_HERE", "DATABASE_NAME");

// create database repository factory
var databaseRepositoryFactory = new DatabaseRepositoryFactory(databaseClientFactory);

// initialize a repository instance
var repository = databaseRepositoryFactory.CreateInstance<IUserRepository>();
```

#### Shortcuts

##### CosmosDB repository factory

```csharp
var repository = CosmosDatabaseRepositoryFactory.CreateInstance<IUserRepository>(
                "CONNECTION_STRING_HERE",
                "DATABASE_NAME",
                true);
```

##### InMemory repository factory

```csharp
var repository = InMemoryDatabaseRepositoryFactory.CreateInstance<IUserRepository>();
```

### Dependency Injection

```csharp
// create database client factory
IDatabaseClientFactory databaseClientFactory = new CosmosDatabaseClientFactory("CONNECTION_STRING_HERE", "DATABASE_NAME");

// add repository instance to DI
services
  .AddDatabase(databaseClientFactory)
  .AddRepository<IUserRepository>()
  .AddRepository<ITenantRepository>();
```
