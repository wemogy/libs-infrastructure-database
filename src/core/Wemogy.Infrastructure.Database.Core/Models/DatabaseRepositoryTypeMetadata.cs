using System;
using System.Linq;

namespace Wemogy.Infrastructure.Database.Core.Models;

public class DatabaseRepositoryTypeMetadata
{
    public DatabaseRepositoryTypeMetadata(Type databaseRepositoryType)
    {
        DatabaseRepositoryType = databaseRepositoryType;
        EntityType = GetEntityType(databaseRepositoryType);
    }

    public Type DatabaseRepositoryType { get; }
    public Type EntityType { get; }

    private static Type GetGenericDatabaseRepositoryType(Type databaseRepositoryType)
    {
        return databaseRepositoryType.GetInterfaces().First(x => x.GenericTypeArguments.Length == 1);
    }

    private static Type GetEntityType(Type databaseRepositoryType)
    {
        return GetGenericDatabaseRepositoryType(databaseRepositoryType).GenericTypeArguments[0];
    }
}
