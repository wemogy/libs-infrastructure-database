using System;
using System.Linq;
using Wemogy.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.Extensions;

public static class TypeExtensions
{
    public static void ThrowIfNotSoftDeletable(this Type entityType)
    {
        if (!entityType.IsSoftDeletable())
        {
            throw Error.Unexpected(
                "ISoftDeletableNotImplemented",
                $"Type {entityType.FullName} does not have a property with the [SoftDeleteFlag] attribute.");
        }
    }

    public static bool IsSoftDeletable(this Type entityType)
    {
        var softDeleteFlagAttributeType = typeof(SoftDeleteFlagAttribute);
        return entityType
            .GetProperties()
            .Any(
                x => x.GetCustomAttributes(true).Any(a => a.GetType() == softDeleteFlagAttributeType));
    }
}
