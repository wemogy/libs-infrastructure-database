namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public class CosmosDatabaseClientOptions
    {
        public CosmosDatabaseClientOptions(string databaseName, string containerName)
        {
            DatabaseName = databaseName;
            ContainerName = containerName;
        }

        public string DatabaseName { get; }
        public string? ContainerName { get; }
    }
}
