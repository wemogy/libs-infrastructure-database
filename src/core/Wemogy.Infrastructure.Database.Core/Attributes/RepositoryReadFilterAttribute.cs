using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

/// <summary>
///     CAUTION! TODO: Ensure that it gets properly implemented
/// </summary>
[AttributeUsage(
    AttributeTargets.Interface,
    AllowMultiple = true)]
public class RepositoryReadFilterAttribute : Attribute
{
    public RepositoryReadFilterAttribute(params Type[] filterTypes)
    {
        FilterTypes = filterTypes;
    }

    public Type[] FilterTypes { get; }
}
