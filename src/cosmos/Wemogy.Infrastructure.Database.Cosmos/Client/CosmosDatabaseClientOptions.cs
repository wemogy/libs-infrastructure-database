namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    public class CosmosDatabaseClientOptions
    {
        /// <summary>
        ///     The container the change feed processors keep their leases in when none is configured.
        /// </summary>
        public const string DefaultLeaseContainerName = "leases";

        public CosmosDatabaseClientOptions(
            string databaseName,
            string containerName,
            string leaseContainerName = DefaultLeaseContainerName)
        {
            DatabaseName = databaseName;
            ContainerName = containerName;
            LeaseContainerName = leaseContainerName;
        }

        public string DatabaseName { get; }
        public string? ContainerName { get; }

        /// <summary>
        ///     The container the change feed processors of this client keep their leases in. One
        ///     container serves every processor of the database: a lease is filed under the name of
        ///     the processor that owns it, so processors of different collections do not collide.
        ///     <para>
        ///         It has to exist with the partition key path <c>/id</c> before a processor is
        ///         started - the provider does not create it, since creating a container needs a
        ///         throughput decision the library has no business making.
        ///     </para>
        /// </summary>
        public string LeaseContainerName { get; }
    }
}
