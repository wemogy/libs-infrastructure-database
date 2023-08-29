# Sorting & Pagination

The DatabaseRepository methods `IterateAsync` and `QueryAsync` support sorting and pagination to allow the user to retrieve a subset of the results.

## Sorting

The generic `Sorting<>` objects allows you to define a sorting on a given properties (multiple properties helps you e.g. to order first on firstname and for all entities which have the same firstname, you can define that they should be ordered by the id property). The sorting can be ascending or descending.

```csharp
var sorting = new Sorting<User>()
  .OrderBy(x => x.FirstName)
  .OrderByDescending(x => x.Id);

var sortedUsers = _userRepository.QueryAsync(
  x => true,
  sorting);
```

## Pagination

The `Pagination` objects allows you to define skip and take values to retrieve a subset of the results.

```csharp
var pagination = new Pagination()
  .Skip(10)
  .Take(10);

var pagedUsers = _userRepository.QueryAsync(
  x => true,
  pagination);
```

## Sorting & Pagination

The `IterateAsync` and `QueryAsync` methods support sorting and pagination. You can combine both to retrieve a subset of the results in a sorted order.

```csharp
var sorting = new Sorting<User>()
  .OrderBy(x => x.FirstName)
  .OrderByDescending(x => x.Id);

var pagination = new Pagination()
  .Skip(10)
  .Take(10);

var sortedPagedUsers = _userRepository.QueryAsync(
  x => true,
  sorting,
  pagination);
```
