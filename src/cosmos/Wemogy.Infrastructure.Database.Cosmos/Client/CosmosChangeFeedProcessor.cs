using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    /// <summary>
    ///     Wraps the change feed processor of the Cosmos SDK, which owns the leases, the
    ///     checkpointing and the load balancing between the instances sharing a processor name.
    /// </summary>
    internal class CosmosChangeFeedProcessor : IChangeFeedProcessor
    {
        private readonly ChangeFeedProcessor _processor;
        private readonly string _databaseName;
        private readonly string _containerName;
        private readonly string _leaseContainerName;
        private readonly string _processorName;

        /// <summary>
        ///     Guards the running flag. Start and stop are not expected to race, but a caller
        ///     disposing a processor while another path stops it should not reach the SDK twice.
        /// </summary>
        private readonly object _gate = new object();

        private bool _isRunning;

        public CosmosChangeFeedProcessor(
            ChangeFeedProcessor processor,
            string processorName,
            string databaseName,
            string containerName,
            string leaseContainerName)
        {
            _processor = processor;
            _processorName = processorName;
            _databaseName = databaseName;
            _containerName = containerName;
            _leaseContainerName = leaseContainerName;
        }

        public async Task StartAsync()
        {
            lock (_gate)
            {
                if (_isRunning)
                {
                    throw ChangeFeedError.AlreadyStarted(_processorName);
                }

                _isRunning = true;
            }

            try
            {
                await _processor.StartAsync();
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                lock (_gate)
                {
                    _isRunning = false;
                }

                // the lease container is the usual one to be missing: the monitored container was
                // created by whoever writes to it, the lease container by nobody
                throw ChangeFeedError.ContainerNotFound(
                    _databaseName,
                    _containerName,
                    _leaseContainerName,
                    e);
            }
            catch
            {
                lock (_gate)
                {
                    _isRunning = false;
                }

                throw;
            }
        }

        public async Task StopAsync()
        {
            lock (_gate)
            {
                if (!_isRunning)
                {
                    return;
                }

                _isRunning = false;
            }

            await _processor.StopAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await StopAsync();
        }
    }
}
