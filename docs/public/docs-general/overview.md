---
slug: /
---

# Overview

Welcome to the Infrastructure Database Libs documentation

## Motivation

The goal of the `wemogy.Infrastructure.Database` library, is to define a unified way to abstract a database. The abstraction should be provider independent, which means that the actual implementation of the `IDatabaseClient` can change without changing the rest of the implementation. Using this approach allows us to declare our interfaces once, generate an actual implementation at runtime and run the same source code against several database providers (e.g. InMemory, Azure Cosmos DB, MongoDB).

Unit-testing database implementations should be automated, regardless of the database provider under test. The tests should be written in an abstract way and should be successful, regardless if a cosmos/mongo-db or in-memory database is being tested.

## Architecture

```mermaid
classDiagram
  OfficialAzureCosmosDbClient <.. CosmosDatabaseClient: use
  OfficialMongoDbClient <.. MongoDatabaseClient: use

  class OfficialAzureCosmosDbClient{
  }
  class CosmosDatabaseClient{
  }


  class OfficialMongoDbClient{
  }
  class MongoDatabaseClient{
  }

  class InMemoryDatabaseClient{
  }

  CosmosDatabaseClient --|> IDatabaseClient : implements
  MongoDatabaseClient --|> IDatabaseClient : implements
  InMemoryDatabaseClient --|> IDatabaseClient : implements

  class IDatabaseClient {
     <<interface>>
  }

  IDatabaseClient <.. DatabaseRepository: use

  class DatabaseRepository {
  }
```
