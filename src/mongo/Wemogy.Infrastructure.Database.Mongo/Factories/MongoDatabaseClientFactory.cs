using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
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

            var settings = MongoClientSettings.FromConnectionString(connectionString);

            if (enableLogging)
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                });
                _logger = loggerFactory.CreateLogger(nameof(MongoDatabaseClientFactory));

                settings.ClusterConfigurator = cb =>
                {
                    cb.Subscribe<CommandStartedEvent>(e =>
                    {
                        _logger.LogInformation(
                            "{CommandName} - {Json}",
                            e.CommandName,
                            e.Command.ToJson());
                    });
                };
            }

            _mongoClient = new MongoClient(settings);
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

        public bool IsMultiTenantDatabaseSupported => false;
    }
}
