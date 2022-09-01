namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public class CosmosDatabaseClientOptions
    {
        public string DatabaseName { get; }
        public string? CustomContainerName { get; }

        public CosmosDatabaseClientOptions(string databaseName, string? customContainerName)
        {
            DatabaseName = databaseName;
            CustomContainerName = customContainerName;
        }

        internal string GetContainerName<TEntity>()
        {
            if (CustomContainerName == null)
            {
                return $"{typeof(TEntity).Name.ToLower()}s";
            }

            return CustomContainerName;
        }
    }
}
