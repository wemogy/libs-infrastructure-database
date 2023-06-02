# Database Repository

## CreateAsync

The `CreateAsync` method is used to insert an entity in the database.

## GetAsync

The `GetAsync` methods provide several ways to get a single entity from the database.
A ```NotFoundErrorException``` exception is thrown if the entity does not exist or if it has been soft-deleted.

## QueryAsync

The `QueryAsync` methods provide several ways to get multiple entities from the database.

## QuerySingleAsync

The `QuerySingleAsync` method provides a way to get a single entity from the database. It throws a ```PreconditionFailedErrorException``` if more results are returned than the expected one. It also throws a ```NotFoundErrorException``` when no result is found.

## ExistsAsync

The `ExistsAsync` methods are used to check if entities exist in the database. They all return true when found or false otherwise.

## EnsureExistsAsync

The `EnsureExistsAsync` methods are used to check if entities exist in the database. They throw a ```NotFoundErrorException``` if the no entities are found.

## IterateAsync

The `IterateAsync` methods are used to iterate the repository via a filter and apply an operation on the filtered results.

## ReplaceAsync

The `ReplaceAsync` method can be used to replace an existing entity in the database.

## UpdateAsync

The `UpdateAsync` method can be used to update an existing entity in the database, through a given operation.
