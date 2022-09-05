using System;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Abstractions;

namespace Wemogy.Infrastructure.Database.Core.Extensions;

public static class TypeExtensions
{
    public static void ThrowIfNotSoftDeletable(this Type entityType)
    {
        if (!entityType.IsSoftDeletable())
        {
            throw Error.Unexpected(
                "ISoftDeletableNotImplemented",
                $"Type {entityType.FullName} does not implement {nameof(ISoftDeletable)}");
        }
    }

    public static bool IsSoftDeletable(this Type entityType)
    {
        return typeof(ISoftDeletable).IsAssignableFrom(entityType);
    }
}
