namespace Wemogy.Infrastructure.Database.Core.Models
{
    public class DatabaseRepositoryOptions
    {
        public bool EnableSoftDelete { get; }

        public DatabaseRepositoryOptions(bool enableSoftDelete)
        {
            EnableSoftDelete = enableSoftDelete;
        }

        public static DatabaseRepositoryOptions Default => new DatabaseRepositoryOptions(false);
    }
}
