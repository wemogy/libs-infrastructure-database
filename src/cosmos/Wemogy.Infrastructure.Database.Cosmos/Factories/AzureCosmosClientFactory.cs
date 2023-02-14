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
        /// <param name="applicationName">The name of application using Azure Monitor or Diagnostics Logs.</param>
        /// <param name="containers">The list of Database Name For Container and Container Name</param>
        /// <param name="insecureDevelopmentMode">
        ///     Skips Certificate checks and uses ConnectionMode.Gateway to enable communication
        ///     with test databases like the CosmosDb Emulator
        /// </param>
        /// <returns>A CosmosClient instance</returns>
        public static CosmosClient FromConnectionString(string connectionString, string applicationName, List<(string, string)> containers, bool insecureDevelopmentMode = false)
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

            return CosmosClient.CreateAndInitializeAsync(connectionString, containers, options).Result;
        }
    }
}
