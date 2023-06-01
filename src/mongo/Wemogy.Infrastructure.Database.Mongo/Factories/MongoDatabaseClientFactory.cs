using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Mongo.Client;

namespace Wemogy.Infrastructure.Database.Mongo.Factories
{
    public class MongoDatabaseClientFactory : IDatabaseClientFactory
    {
        private readonly MongoClient _mongoClient;
        private readonly string _databaseName;
        private readonly ILogger? _logger;

        public MongoDatabaseClientFactory(
            string connectionString,
            string databaseName,
            bool enableLogging = false)
        {
            _databaseName = databaseName;

            // instruct the driver to camelCase the fields in MongoDB
            var pack = new ConventionPack { new CamelCaseElementNameConvention() };
            ConventionRegistry.Register(nameof(CamelCaseElementNameConvention), pack, x => true);


            _mongoClient = new MongoClient(connectionString);

            if (enableLogging)
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                _logger = loggerFactory.CreateLogger(nameof(MongoDatabaseClientFactory));
            }
        }

        public IDatabaseClient<TEntity> CreateClient<TEntity>(DatabaseRepositoryOptions databaseRepositoryOptions)
            where TEntity : class
        {
            var options = new MongoDatabaseClientOptions(
                _databaseName,
                databaseRepositoryOptions.CollectionName);

            return new MongoDatabaseClient<TEntity>(
                _mongoClient,
                options,
                _logger);
        }
    }
}
