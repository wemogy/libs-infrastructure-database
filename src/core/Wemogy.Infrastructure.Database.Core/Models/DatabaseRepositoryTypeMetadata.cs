using System;
using System.Linq;
using Wemogy.Infrastructure.Database.Core.Abstractions;

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
        // Handle the case where the type itself is IDatabaseRepository<T>
        if (databaseRepositoryType.IsGenericType &&
            databaseRepositoryType.GetGenericTypeDefinition() == typeof(IDatabaseRepository<>))
        {
            return databaseRepositoryType;
        }

        return databaseRepositoryType.GetInterfaces().First(x => x.GenericTypeArguments.Length == 1);
    }

    private static Type GetEntityType(Type databaseRepositoryType)
    {
        return GetGenericDatabaseRepositoryType(databaseRepositoryType).GenericTypeArguments[0];
    }
}
