using System;
using Wemogy.Infrastructure.Database.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Core.Models
{
    public class DatabaseRepositoryOptions
    {
        public bool EnableSoftDelete { get; }

        public DatabaseRepositoryOptions(bool enableSoftDelete)
        {
            EnableSoftDelete = enableSoftDelete;
        }

        public static DatabaseRepositoryOptions GetDefault(Type entityType)
        {
            var enableSoftDelete = entityType.IsSoftDeletable();
            return new DatabaseRepositoryOptions(enableSoftDelete);
        }
    }
}
