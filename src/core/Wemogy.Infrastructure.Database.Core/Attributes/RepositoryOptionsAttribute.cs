using System;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class RepositoryOptionsAttribute : Attribute
    {
        public DatabaseRepositoryOptions Options { get; set; }
        public RepositoryOptionsAttribute(bool enableSoftDelete)
        {
            Options = new DatabaseRepositoryOptions(enableSoftDelete);
        }
    }
}
