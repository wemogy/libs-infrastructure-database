using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

[AttributeUsage(
    AttributeTargets.Interface,
    AllowMultiple = true)]
public class RepositoryPropertyFilterAttribute : Attribute
{
    public RepositoryPropertyFilterAttribute(params Type[] filterTypes)
    {
        FilterTypes = filterTypes;
    }

    public Type[] FilterTypes { get; }
}
