using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Interface, AllowMultiple = true)]
    public class RepositoryReadFilterAttribute : Attribute
    {
        public Type[] FilterTypes { get; }
        public RepositoryReadFilterAttribute(params Type[] filterTypes)
        {
            FilterTypes = filterTypes;
        }
    }
}