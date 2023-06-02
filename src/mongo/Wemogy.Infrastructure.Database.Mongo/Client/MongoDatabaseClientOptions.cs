namespace Wemogy.Infrastructure.Database.Mongo.Client
{
    public class MongoDatabaseClientOptions
    {
        public string DatabaseName { get; }
        public string ContainerName { get; }

        public MongoDatabaseClientOptions(string databaseName, string containerName)
        {
            DatabaseName = databaseName;
            ContainerName = containerName;
        }
    }
}
