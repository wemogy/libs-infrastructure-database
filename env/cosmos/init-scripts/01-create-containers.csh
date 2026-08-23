# Seeds the database and containers the integration tests expect. Cosmos Shell
# runs every .csh in /init alphabetically inside a single session, so pass
# --database explicitly to stay independent of any other init script.
mkdb infrastructuredbtests

mkcon users /tenantId --database=infrastructuredbtests
mkcon datacenters /partitionKey --database=infrastructuredbtests
