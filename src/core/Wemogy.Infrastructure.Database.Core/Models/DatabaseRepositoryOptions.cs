namespace Wemogy.Infrastructure.Database.Core.Models;

public class DatabaseRepositoryOptions
{
    public string CollectionName { get; set; }

    public bool EnableSoftDelete { get; }

    public DatabaseRepositoryOptions(
        string collectionName,
        bool enableSoftDelete)
    {
        CollectionName = collectionName;
        EnableSoftDelete = enableSoftDelete;
    }
}
