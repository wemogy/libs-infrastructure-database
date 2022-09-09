using System;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Attributes
{
    [AttributeUsage(AttributeTargets.Interface)]
    public class RepositoryOptionsAttribute : Attribute
    {
        public bool EnableSoftDelete { get; }

        public string? CollectionName { get; }

        public RepositoryOptionsAttribute(bool enableSoftDelete = false, string? collectionName = null)
        {
            EnableSoftDelete = enableSoftDelete;
            CollectionName = collectionName;
        }
    }
}
