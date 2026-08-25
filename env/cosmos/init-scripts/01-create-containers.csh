# Seeds the database and containers the integration tests expect. Cosmos Shell
# runs every .csh in /init alphabetically inside a single session, so pass
# --database explicitly to stay independent of any other init script.
mkdb infrastructuredbtests

mkcon users /tenantId --database=infrastructuredbtests
mkcon datacenters /partitionKey --database=infrastructuredbtests

# the change feed processors keep their leases here; the partition key path has to be /id,
# and the library deliberately does not create the container itself
mkcon leases /id --database=infrastructuredbtests
