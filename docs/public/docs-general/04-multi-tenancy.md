# Multi-Tenancy

## Motivation
For the use-case that you need to support the same primary key values on the same database (for example when different cloned environments are migrated on the same database), the concept of multi-tenancy is used.

The idea is that the migration takes place by prefixing all partition keys by an environment/tenant-related value. The prefix is injected when querying/persisting and is filtered out of the results.

### Advantages

#### Secure by default

Using this approach the repository implementation, which automatically prefix the `GetTenantId()` result of the `IDatabaseTenantProvider`, makes it **impossible to access the data** of another database tenant accidentally.

## How to Use

1. Create an IDatabaseTenantProvider:

```csharp title='Example for IDatabaseTenantProvider'
using System;
using SpaceBlocks.Libs.Core.Context.Abstractions;
using Wemogy.Infrastructure.Database.Core.Plugins.ComposedPrimaryKey.Abstractions;

namespace Wemogy.Subscriptions.Webservices.Main.Core
{
    public class AppleTenantProvider : IDatabaseTenantProvider
    {
        public string GetTenantId() => "apple_production";
    }
}

```

2. Adapt your DI container to use the provider above (unit-test example):

```csharp title='DI example for IDatabaseTenantProvider'
[Fact]
public void AddMultiTenantDatabaseRepository_ShouldWork()
{
    // Arrange
    var cosmosDatabaseClientFactory = new CosmosDatabaseClientFactory(
        ConnectionString,
        DatabaseName);
    ServiceCollection.AddSingleton<AppleTenantProvider>();
    ServiceCollection
        .AddDatabase(cosmosDatabaseClientFactory)
        .AddRepository<IUserRepository, AppleTenantProvider>();
    // Act
    var userRepository = ServiceCollection.BuildServiceProvider().GetRequiredService<IUserRepository>();
    // Assert
    Assert.NotNull(userRepository);

    var entity = await userRepository.CreateAsync(user)
    // entity.tenantId is not prefixed with apple_production. But in the database it is.
    // the library filters out the prefixes when retrieving data from the database. Those are only used internally to
    // support the multi-tenancy concept.
}
```

The result is indexed under the ```apple_production__{ID}``` partition key and the tenantId property has the same value as the partition key.
When querying/getting the entity out of the repository, the prefix is automatically removed.

Predicate support is also included, meaning that you can use the partition-key as filter in a predicate and the prefixing is automatically done via the library as well.
