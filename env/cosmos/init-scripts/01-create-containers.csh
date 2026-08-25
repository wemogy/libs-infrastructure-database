# Seeds the database and containers the integration tests expect. Cosmos Shell
# runs every .csh in /init alphabetically inside a single session, so pass
# --database explicitly to stay independent of any other init script.
mkdb infrastructuredbtests

mkcon users /tenantId --database=infrastructuredbtests
mkcon datacenters /partitionKey --database=infrastructuredbtests

# comma-separated paths define a hierarchical partition key, ordered from the broadest
# component to the narrowest - the order the entity numbers its components in
mkcon usageevents /customerId,/meterSlug,/timeBucket --database=infrastructuredbtests
