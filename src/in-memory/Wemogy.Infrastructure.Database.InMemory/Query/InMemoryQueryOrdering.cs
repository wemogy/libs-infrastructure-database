using System;
using System.Collections.Generic;
using System.Linq;
using FastExpressionCompiler;
using Newtonsoft.Json;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Extensions;

namespace Wemogy.Infrastructure.Database.InMemory.Query
{
    /// <summary>
    ///     Applies the ordering part of <see cref="QueryParameters"/> to an already materialized
    ///     result set: the sortings themselves and the keyset cursor built from their
    ///     <see cref="QuerySorting.SearchAfter"/> values.
    ///     <para>
    ///         Providers that translate to a query language get this from the database. The
    ///         in-memory client has to do it itself, and it has to do it across all partitions
    ///         rather than per partition, otherwise the order would depend on how the entities are
    ///         distributed.
    ///     </para>
    /// </summary>
    public static class InMemoryQueryOrdering
    {
        /// <summary>
        ///     Orders the entities by every sorting of the query parameters, in the order they are
        ///     declared. Returns the input unchanged if there are no sortings.
        /// </summary>
        public static List<T> ApplySortings<T>(List<T> entities, QueryParameters queryParameters)
        {
            if (!queryParameters.ContainsSortings)
            {
                return entities;
            }

            IOrderedEnumerable<T>? ordered = null;

            foreach (var sorting in queryParameters.Sortings)
            {
                var keySelector = KeySelector<T>(sorting);

                if (ordered == null)
                {
                    ordered = sorting.IsAscending
                        ? entities.OrderBy(keySelector)
                        : entities.OrderByDescending(keySelector);
                }
                else
                {
                    ordered = sorting.IsAscending
                        ? ordered.ThenBy(keySelector)
                        : ordered.ThenByDescending(keySelector);
                }
            }

            return ordered!.ToList();
        }

        /// <summary>
        ///     Keeps only the entities that come after the keyset cursor in the current sort order,
        ///     so the caller continues after the entity the previous page ended on. Only the leading
        ///     sortings that carry a <see cref="QuerySorting.SearchAfter"/> take part in the cursor,
        ///     which mirrors how the SQL based providers build their tie-breaker chain.
        ///     Returns the input unchanged if no sorting carries a cursor.
        /// </summary>
        public static List<T> ApplySearchAfter<T>(List<T> entities, QueryParameters queryParameters)
        {
            var cursor = queryParameters.Sortings
                .TakeWhile(x => x.ContainsSearchAfter)
                .Select(
                    x => new SearchAfterColumn<T>(
                        KeySelector<T>(x),
                        DeserializeSearchAfter<T>(x),
                        x.IsAscending))
                .ToList();

            if (cursor.Count == 0)
            {
                return entities;
            }

            return entities
                .Where(entity => IsAfterCursor(entity, cursor))
                .ToList();
        }

        /// <summary>
        ///     Lexicographic comparison of the entity's sort key against the cursor: the first
        ///     column that differs decides, equal columns fall through to the next one. An entity
        ///     whose whole sort key equals the cursor is the entity the previous page ended on and
        ///     is therefore excluded.
        /// </summary>
        private static bool IsAfterCursor<T>(T entity, List<SearchAfterColumn<T>> cursor)
        {
            foreach (var column in cursor)
            {
                var comparison = Comparer<object>.Default.Compare(
                    column.KeySelector(entity),
                    column.Value);

                if (comparison == 0)
                {
                    continue;
                }

                return column.IsAscending ? comparison > 0 : comparison < 0;
            }

            return false;
        }

        private static Func<T, object> KeySelector<T>(QuerySorting sorting)
        {
            return sorting.GetOrderByExpression<T>().CompileFast();
        }

        private static object? DeserializeSearchAfter<T>(QuerySorting sorting)
        {
            var propertyType = QueryParametersExtensions.ResolvePropertyType<T>(sorting.OrderBy.ToPascalCase());
            return JsonConvert.DeserializeObject(
                sorting.SearchAfter!,
                propertyType);
        }

        private sealed class SearchAfterColumn<T>
        {
            public SearchAfterColumn(Func<T, object> keySelector, object? value, bool isAscending)
            {
                KeySelector = keySelector;
                Value = value;
                IsAscending = isAscending;
            }

            public Func<T, object> KeySelector { get; }

            public object? Value { get; }

            public bool IsAscending { get; }
        }
    }
}
