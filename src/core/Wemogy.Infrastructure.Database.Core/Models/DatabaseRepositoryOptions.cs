namespace Wemogy.Infrastructure.Database.Core.Models;

public class DatabaseRepositoryOptions
{
    public DatabaseRepositoryOptions(
        string collectionName,
        bool enableSoftDelete)
    {
        CollectionName = collectionName;
        EnableSoftDelete = enableSoftDelete;
    }

    public string CollectionName { get; set; }

    public bool EnableSoftDelete { get; }
}
