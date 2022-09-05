using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

[AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
public class RepositoryPropertyFilterAttribute : Attribute
{
    public Type[] FilterTypes { get; }
    public RepositoryPropertyFilterAttribute(params Type[] filterTypes)
    {
        FilterTypes = filterTypes;
    }
}
