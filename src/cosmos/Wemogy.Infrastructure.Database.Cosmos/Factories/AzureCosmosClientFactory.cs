using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Azure.Cosmos;

namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    /// <summary>
    /// This factory creates a plain CosmosClient from the .NET SDK.
    /// </summary>
    public static class AzureCosmosClientFactory
    {
        /// <summary>
        ///     Creates a CosmosClient with out default CosmosClientOptions like camel case
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <param name="insecureDevelopmentMode">
        ///     Skips Certificate checks and uses ConnectionMode.Gateway to enable communication
        ///     with test databases like the CosmosDb Emulator
        /// </param>
        /// <param name="containers">
        ///     The list of pairs of Database and Container, which should identify which is/are the container/s your current application will commonly interact with during its lifetime.
        ///     This could be all or a subset of the containers in your Cosmos DB account. Set this to improve Cosmos DB initialization performance.
        /// </param>
        /// <param name="applicationName">The name of application using Azure Monitor or Diagnostics Logs.</param>
        /// <returns>A CosmosClient instance</returns>
        public static CosmosClient FromConnectionString(string connectionString, bool insecureDevelopmentMode = false, List<(string, string)>? containers = null, string? applicationName = null)
        {
            var options = new CosmosClientOptions
            {
                ApplicationName = applicationName,
                SerializerOptions = new CosmosSerializationOptions
                {
                    IgnoreNullValues = true,
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                },
            };

            if (insecureDevelopmentMode)
            {
                options.ConnectionMode = ConnectionMode.Gateway;
                options.HttpClientFactory = () =>
                {
                    HttpMessageHandler httpMessageHandler = new HttpClientHandler
                    {
                        ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
                    };
                    return new HttpClient(httpMessageHandler);
                };
            }

            if (containers == null)
            {
                return new CosmosClient(connectionString, options);
            }

            return CosmosClient.CreateAndInitializeAsync(connectionString, containers, options).Result;
        }
    }
}
