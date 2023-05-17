# ![wemogy logo](https://wemogyimages.blob.core.windows.net/logos/wemogy-github-tiny.png) Infrastructure Database Layer

Abstraction Layer for multiple Database Technologies.

Currently Supported:

- Local In Memory Database
- Azure Cosmos DB

## Getting started

### Local In Memory Database

Install the [NuGet package](https://www.nuget.org/packages/Wemogy.Infrastructure.Database.Cosmos) into your project.

```bash
Wemogy.Infrastructure.Database.Cosmos
```

### Azure Cosmos DB

Install the [NuGet package](https://www.nuget.org/packages/Wemogy.Infrastructure.Database.InMemory) into your project.

```bash
Wemogy.Infrastructure.Database.InMemory
```

Checkout the [Documentation](http://libs-infrastructure-database.docs.wemogy.com/) to get information about the available classes and extensions.

## Testing

### Cosmos

#### Initialize & start the cosmos DB emulator

```bash
docker-compose -f env/cosmos/docker-compose.yaml up
```
