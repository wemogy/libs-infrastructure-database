using System;
using System.Linq;

namespace Wemogy.Infrastructure.Database.Core.Models;

public class DatabaseRepositoryTypeMetadata
{
    private static Type GetGenericDatabaseRepositoryType(Type databaseRepositoryType)
    {
        return databaseRepositoryType.GetInterfaces().First(x => x.GenericTypeArguments.Length == 3);
    }

    private static Type GetEntityType(Type databaseRepositoryType)
    {
        return GetGenericDatabaseRepositoryType(databaseRepositoryType).GenericTypeArguments[0];
    }

    private static Type GetPartitionKeyType(Type databaseRepositoryType)
    {
        return GetGenericDatabaseRepositoryType(databaseRepositoryType).GenericTypeArguments[1];
    }

    private static Type GetIdType(Type databaseRepositoryType)
    {
        return GetGenericDatabaseRepositoryType(databaseRepositoryType).GenericTypeArguments[2];
    }

    public Type EntityType { get; }
    public Type PartitionKeyType { get; }
    public Type IdType { get; }

    public DatabaseRepositoryTypeMetadata(Type databaseRepositoryType)
    {
        EntityType = GetEntityType(databaseRepositoryType);
        PartitionKeyType = GetPartitionKeyType(databaseRepositoryType);
        IdType = GetIdType(databaseRepositoryType);
    }
}
