namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public class CosmosDatabaseClientOptions
    {
        public string DatabaseName { get; }
        public string? ContainerName { get; }

        public CosmosDatabaseClientOptions(string databaseName, string containerName)
        {
            DatabaseName = databaseName;
            ContainerName = containerName;
        }
    }
}
