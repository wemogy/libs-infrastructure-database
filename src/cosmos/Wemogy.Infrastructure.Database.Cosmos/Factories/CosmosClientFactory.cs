using System.Net.Http;
using System.Runtime.CompilerServices;
using Microsoft.Azure.Cosmos;

[assembly:InternalsVisibleTo("Wemogy.Infrastructure.Database.Cosmos.UnitTests")]
namespace Wemogy.Infrastructure.Database.Cosmos.Factories
{
    internal static class CosmosClientFactory
    {
        /// <summary>
        ///     Creates a CosmosClient with out default CosmosClientOptions like camel case
        /// </summary>
        /// <param name="connectionString">The connection string</param>
        /// <param name="insecureDevelopmentMode">
        ///     Skips Certificate checks and uses ConnectionMode.Gateway to enable communication
        ///     with test databases like the CosmosDb Emulator
        /// </param>
        /// <returns>A CosmosClient instance</returns>
        public static CosmosClient FromConnectionString(string connectionString, bool insecureDevelopmentMode = false)
        {
            var options = new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    IgnoreNullValues = true,
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
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

            return new CosmosClient(
                connectionString,
                options);
        }
    }
}
