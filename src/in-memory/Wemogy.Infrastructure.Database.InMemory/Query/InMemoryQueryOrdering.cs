using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.Json;
using FastExpressionCompiler;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Serialization;
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
                        ? entities.OrderBy(
                            keySelector,
                            SortKeyComparer.Instance)
                        : entities.OrderByDescending(
                            keySelector,
                            SortKeyComparer.Instance);
                }
                else
                {
                    ordered = sorting.IsAscending
                        ? ordered.ThenBy(
                            keySelector,
                            SortKeyComparer.Instance)
                        : ordered.ThenByDescending(
                            keySelector,
                            SortKeyComparer.Instance);
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
                var comparison = SortKeyComparer.Instance.Compare(
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

        /// <summary>
        ///     Builds the sort key accessor for the sorting's property path. Unlike
        ///     <c>GetOrderByExpression</c> it guards every intermediate step of the path, so an
        ///     entity whose nested property is null sorts as a null key instead of throwing. That
        ///     matches the SQL based providers, where a missing path is simply undefined.
        /// </summary>
        private static Func<T, object?> KeySelector<T>(QuerySorting sorting)
        {
            var parameter = Expression.Parameter(
                typeof(T),
                "x");
            var propertyMembers = sorting.OrderBy.Split('.');

            Expression body = parameter;
            Expression? guard = null;

            for (var i = 0; i < propertyMembers.Length; i++)
            {
                // the parameter itself is never null, so only the intermediate results need a guard
                if (i > 0 && CanBeNull(body.Type))
                {
                    var notNull = Expression.NotEqual(
                        body,
                        Expression.Constant(
                            null,
                            body.Type));
                    guard = guard == null
                        ? notNull
                        : Expression.AndAlso(
                            guard,
                            notNull);
                }

                body = Expression.PropertyOrField(
                    body,
                    propertyMembers[i]);
            }

            Expression key = Expression.Convert(
                body,
                typeof(object));

            if (guard != null)
            {
                key = Expression.Condition(
                    guard,
                    key,
                    Expression.Constant(
                        null,
                        typeof(object)));
            }

            return Expression.Lambda<Func<T, object?>>(
                    key,
                    parameter)
                .CompileFast();
        }

        private static bool CanBeNull(Type type)
        {
            return !type.IsValueType || Nullable.GetUnderlyingType(type) != null;
        }

        private static object? DeserializeSearchAfter<T>(QuerySorting sorting)
        {
            var propertyType = QueryParametersExtensions.ResolvePropertyType<T>(sorting.OrderBy.ToPascalCase());
            return JsonSerializer.Deserialize(
                sorting.SearchAfter!,
                propertyType,
                DatabaseJson.QueryValueOptions);
        }

        /// <summary>
        ///     Compares sort keys the way the SQL based providers do: strings ordinally rather than
        ///     by the current culture. <see cref="Comparer{T}.Default"/> would delegate to
        ///     <see cref="string.CompareTo(string)"/>, which is culture sensitive and would make
        ///     both the order and every page depend on the culture of the running machine.
        /// </summary>
        private sealed class SortKeyComparer : IComparer<object?>
        {
            public static readonly SortKeyComparer Instance = new SortKeyComparer();

            public int Compare(object? x, object? y)
            {
                if (x is string left && y is string right)
                {
                    return string.CompareOrdinal(
                        left,
                        right);
                }

                return Comparer<object>.Default.Compare(
                    x,
                    y);
            }
        }

        private sealed class SearchAfterColumn<T>
        {
            public SearchAfterColumn(Func<T, object?> keySelector, object? value, bool isAscending)
            {
                KeySelector = keySelector;
                Value = value;
                IsAscending = isAscending;
            }

            public Func<T, object?> KeySelector { get; }

            public object? Value { get; }

            public bool IsAscending { get; }
        }
    }
}
