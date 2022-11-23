using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

[AttributeUsage(AttributeTargets.Interface)]
public class RepositoryOptionsAttribute : Attribute
{
    public RepositoryOptionsAttribute(bool enableSoftDelete = false, string? collectionName = null)
    {
        EnableSoftDelete = enableSoftDelete;
        CollectionName = collectionName;
    }

    public bool EnableSoftDelete { get; }

    public string? CollectionName { get; }
}
