# ![wemogy logo](https://wemogyimages.blob.core.windows.net/logos/wemogy-github-tiny.png) Infrastructure Database Layer

Abstraction Layer for multiple Database Technologies.

Currently Supported:

- Local In Memory Database
- Azure Cosmos DB
- MongoDB

## Getting started

### Local In Memory Database

Install the [NuGet package](https://www.nuget.org/packages/Wemogy.Infrastructure.Database.InMemory) into your project.

```bash
dontet add package Wemogy.Infrastructure.Database.InMemory
```

Initialize the Database Client Factory centrally.

```csharp
var databaseClientFactory = new InMemoryDatabaseClientFactory();
```

### Azure Cosmos DB

Install the [NuGet package](https://www.nuget.org/packages/Wemogy.Infrastructure.Database.Cosmos) into your project.

```bash
dontet add package Wemogy.Infrastructure.Database.Cosmos
```

Initialize the Database Client Factory centrally.

```csharp
var databaseClientFactory = new CosmosDatabaseClientFactory("<CONNECTION_STRING>", "<DATABASE_NAME>");
```

### MongoDB

Install the [NuGet package](https://www.nuget.org/packages/Wemogy.Infrastructure.Database.Mongo) into your project.

```bash
dontet add package Wemogy.Infrastructure.Database.Mongo
```

Initialize the Database Client Factory centrally.

```csharp
var databaseClientFactory = new MongoDatabaseClientFactory("<CONNECTION_STRING>", "<DATABASE_NAME>");
```

## Define and register Repositories

If your Models are stored in a differen class library, install the Core [NuGet package](https://www.nuget.org/packages/Wemogy.Infrastructure.Database.Core) into your project.

```bash
dontet add package Wemogy.Infrastructure.Database.Core
```

Define a class that you want to store in a database vie the Repository, and let it inherit from `EntityBase`.

```csharp
public class Foo : EntityBase
{
}
```

Each repository needs to implement the `IDatabaseRepository<T>` interface.

```csharp
public interface IFooRepository : IDatabaseRepository<Foo>
{
}
```

Register the Database Client Factory and the Repositories centrally at the Dependency Injection Container (for example in `Startup.cs`).

```csharp
var databaseClientFactory = new // ...

services
    .AddDatabase(databaseClientFactory)
    .AddRepository<IFooRepository>()
    .AddRepository<IBarRepository>();
```

---

Checkout the full [Documentation](http://libs-infrastructure-database.docs.wemogy.com/) to get information about the available classes and extensions.



## Testing

### Cosmos

#### Initialize & start the cosmos DB emulator

```bash
docker compose -f env/cosmos/docker-compose.yaml up
```

### MongoDB

#### Initialize & start the MongoDB emulator

```bash
docker compose -f env/mongo/docker-compose.yaml up
```
