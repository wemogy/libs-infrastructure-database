using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.Cosmos.Helpers;
using Wemogy.Infrastructure.Database.Cosmos.Models;

#pragma warning disable CS8602

// ReSharper disable All

namespace Wemogy.Infrastructure.Database.Cosmos.Extensions
{
    public static class QueryParametersExtensions
    {
        public static Expression<Func<T, bool>> GetSearchAfterExpression<T>(this QuerySorting querySorting)
        {
            // Thanks to: https://stackoverflow.com/questions/48954125/c-sharp-how-to-evaluate-compareto-in-expression-which-returns-a-string

            var propertyName = querySorting.OrderBy.ToPascalCase();

            // x =>
            var param = Expression.Parameter(
                typeof(T),
                "x");

            // x.PropertyNameA.PropertyNameB
            var propertyExpression = GetPropertyExpression(
                propertyName,
                param);

            var propertyType = ResolvePropertyType<T>(propertyName);
            var searchAfterValue = JsonConvert.DeserializeObject(
                querySorting.SearchAfter!,
                propertyType);

            MethodInfo? comparisonMethod = null;
            Expression searchAfterValueExpression = Expression.Constant(searchAfterValue);

            // CompareTo is not working for GUID, for that reason we handle the GUID as string in comparison
            if (propertyType == typeof(Guid))
            {
                comparisonMethod = typeof(string).GetMethod(
                    nameof(string.CompareTo),
                    new[] { typeof(string) });

                // ToString() without parameters is declared on every type, so the lookup holds
                var guidToStringMethod = propertyType.GetMethod(
                    nameof(string.ToString),
                    new Type[0])!;
                propertyExpression = Expression.Call(
                    propertyExpression,
                    guidToStringMethod);
                searchAfterValueExpression = Expression.Call(
                    searchAfterValueExpression,
                    guidToStringMethod);
            }
            else if (propertyType == typeof(DateTime))
            {
                // DateTime is supported by Expression.GreaterThan
            }
            else if (propertyType == typeof(JValue))
            {
                comparisonMethod = typeof(string).GetMethod(
                    nameof(string.CompareTo),
                    new[] { typeof(string) });
                var jValueToStringMethod = typeof(JValue).GetMethod(
                    nameof(string.ToString),
                    new Type[0])!;
                propertyExpression = Expression.Call(
                    propertyExpression,
                    jValueToStringMethod);
                searchAfterValueExpression = Expression.Call(
                    searchAfterValueExpression,
                    jValueToStringMethod);
            }
            else
            {
                comparisonMethod = propertyType.GetMethod(
                    nameof(string.CompareTo),
                    new[] { propertyType });
            }

            // the cursor has to move in the direction the column is ordered in. Comparing with
            // "greater than" for a descending column returns the half of the result set the caller
            // has already paged through.
            Expression searchExpr;
            if (comparisonMethod == null)
            {
                searchExpr = querySorting.IsAscending
                    ? Expression.GreaterThan(
                        propertyExpression,
                        searchAfterValueExpression)
                    : Expression.LessThan(
                        propertyExpression,
                        searchAfterValueExpression);
            }
            else
            {
                Expression callExpr = Expression.Call(
                    propertyExpression,
                    comparisonMethod,
                    searchAfterValueExpression);
                searchExpr = querySorting.IsAscending
                    ? Expression.GreaterThan(
                        callExpr,
                        Expression.Constant(0))
                    : Expression.LessThan(
                        callExpr,
                        Expression.Constant(0));
            }

            var myLambda =
                Expression.Lambda<Func<T, bool>>(
                    searchExpr,
                    param);

            return myLambda;
        }

        public static Expression<Func<T, object>> GetOrderByExpression<T>(this QuerySorting querySorting)
        {
            return GetXPropertyExpression<T>(querySorting.OrderBy);
        }

        public static Expression<Func<T, bool>> GetLambdaExpression<T>(this QueryParameters queryParameters)
        {
            return BuildExpressionTree<T>(queryParameters.Filters);
        }

        public static Expression<Func<T, bool>> BuildExpressionTree<T>(List<QueryFilter> queryFilters)
        {
            // x =>
            var param = Expression.Parameter(
                typeof(T),
                "x");

            if (queryFilters.Count == 0)
            {
                return Expression.Lambda<Func<T, bool>>(
                    Expression.Constant(
                        true,
                        typeof(bool)),
                    param);
            }

            // create all expressions
            var queryFilterExpressions = queryFilters
                .Select(
                    x => new KeyValuePair<QueryFilter, Expression>(
                        x,
                        GetQueryFilterExpression<T>(
                            x,
                            param)))
                .ToDictionary(
                    x => x.Key,
                    x => x.Value);

            // create a node for each expression group
            var previousLevelGroupsExpressions = new Dictionary<int, List<Expression>>();

            // get levels and order them from bottom to top
            var levels = queryFilters.GroupBy(x => x.LevelId).OrderByDescending(x => x.Key);

            // build the nodes for each level
            foreach (var level in levels)
            {
                var groupsOfLevel = level.GroupBy(x => x.GroupId);
                foreach (var group in groupsOfLevel)
                {
                    var groupId = group.Key;
                    var parentGroupId = group.First().ParentGroupId;
                    var itemsInGroup = group.Count();
                    var subLevelGroupExpressions =
                        previousLevelGroupsExpressions.Get(groupId) ?? new List<Expression>();
                    previousLevelGroupsExpressions.Remove(groupId);
                    var hasSubLevelGroupExpressions = subLevelGroupExpressions.Any();

                    if (itemsInGroup == 1 && !hasSubLevelGroupExpressions)
                    {
                        previousLevelGroupsExpressions.AddItem(
                            parentGroupId,
                            queryFilterExpressions[group.First()]);
                        continue;
                    }

                    var expressionBuilderOfGroup =
                        GetExpressionTreeNodeIdExpressionBuilder(group.First().ExpressionTreeNodeId);

                    if (itemsInGroup == 1 && subLevelGroupExpressions.Count == 1)
                    {
                        previousLevelGroupsExpressions.AddItem(
                            parentGroupId,
                            expressionBuilderOfGroup(
                                queryFilterExpressions[group.First()],
                                subLevelGroupExpressions.First()));
                        continue;
                    }

                    var groupItems = group.ToList();

                    var partialExpression = expressionBuilderOfGroup(
                        queryFilterExpressions[groupItems[0]],
                        queryFilterExpressions[groupItems[1]]);

                    for (var i = 2; i < groupItems.Count; i++)
                    {
                        partialExpression =
                            expressionBuilderOfGroup(
                                partialExpression,
                                queryFilterExpressions[groupItems[i]]);
                    }

                    foreach (var subLevelGroupExpression in subLevelGroupExpressions)
                    {
                        partialExpression = expressionBuilderOfGroup(
                            partialExpression,
                            subLevelGroupExpression);
                    }

                    previousLevelGroupsExpressions.AddItem(
                        parentGroupId,
                        partialExpression);
                }
            }

            return Expression.Lambda<Func<T, bool>>(
                previousLevelGroupsExpressions.First().Value.First(),
                param);
        }

        public static Type ResolvePropertyType<T>(string propertyName)
        {
            return ResolvePropertyType(
                propertyName,
                typeof(T));
        }

        public static Type ResolvePropertyType(string propertyName, Type type)
        {
            var propertyPath = propertyName.Split('.');
            if (propertyPath.Length == 0)
            {
                throw new Exception($"propertyPath for property {propertyName} is not valid!");
            }

            var currentPropertyName = propertyPath.First();
            var propertyInfo = type.GetProperty(currentPropertyName);

            if (propertyInfo == null)
            {
                throw new Exception($"could not find property {currentPropertyName} in type {type.FullName}");
            }

            if (propertyPath.Length > 1)
            {
                var subPropertyName = string.Join(
                    ".",
                    propertyPath.Skip(1));
                return ResolvePropertyType(
                    subPropertyName,
                    propertyInfo.PropertyType);
            }

            return propertyInfo.PropertyType;
        }

        public static UnaryExpression GetValueExpression<T>(string propertyName, string value, Comparator comparator)
        {
            // resolve the type
            var valueType = ResolvePropertyType<T>(propertyName);

            if (comparator == Comparator.IsOneOf)
            {
                var iEnumerable = typeof(List<>);

                valueType = iEnumerable.MakeGenericType(valueType);
            }
            else if (comparator == Comparator.Contains)
            {
                valueType = valueType.GenericTypeArguments.FirstOrDefault() ?? valueType;
            }

            // JsonConvert.Deserialize
            var valueObj = JsonConvert.DeserializeObject(
                value,
                valueType);

            var constant = Expression.Constant(valueObj);
            return Expression.Convert(
                constant,
                valueType);
        }

        /// <summary>
        ///     e.g. x.PropA.PropB.PropC
        /// </summary>
        public static Expression GetPropertyExpression(string propertyName, ParameterExpression parameterExpression)
        {
            Expression body = parameterExpression;

            foreach (var member in propertyName.Split('.'))
            {
                body = Expression.PropertyOrField(
                    body,
                    member);
            }

            return body;
        }

        public static Expression<Func<T, object>> GetXPropertyExpression<T>(string propertyName)
        {
            // x =>
            var param = Expression.Parameter(
                typeof(T),
                "x");

            // This not work! ToDo: ThenBy support https://stackoverflow.com/questions/1689199/c-sharp-code-to-order-by-a-property-using-the-property-name-as-a-string (3. Answer)
            var property = GetPropertyExpression(
                propertyName,
                param);
            var propAsObject = Expression.Convert(
                property,
                typeof(object));

            return Expression.Lambda<Func<T, dynamic>>(
                propAsObject,
                param);
        }

        /// <summary>
        ///     e.g. <c>$x.Folder != null &amp;&amp; ($x.Folder).Parent != null &amp;&amp; target</c>
        /// </summary>
        public static Expression AddPropertyNullCheckExpression(
            string propertyName,
            ParameterExpression parameterExpression,
            Expression target)
        {
            Expression body = parameterExpression;

            var nullExpression = Expression.Constant(
                null,
                typeof(object));
            var propertyMembers = propertyName.Split('.');

            // if normal direct property => nothing to do
            if (propertyMembers.Length == 1)
            {
                return target;
            }

            if (propertyMembers.Length == 2)
            {
                return Expression.AndAlso(
                    Expression.NotEqual(
                        Expression.PropertyOrField(
                            body,
                            propertyMembers[0]),
                        nullExpression),
                    target);
            }

            var nullCheckExpression = Expression.AndAlso(
                Expression.NotEqual(
                    Expression.PropertyOrField(
                        body,
                        propertyMembers[0]),
                    nullExpression),
                Expression.NotEqual(
                    NestedPropertyOrField(
                        body,
                        propertyMembers.TakeAsArray(2)),
                    nullExpression));

            for (var i = 2; i < propertyMembers.Length - 1; i++)
            {
                nullCheckExpression = Expression.AndAlso(
                    nullCheckExpression,
                    Expression.NotEqual(
                        NestedPropertyOrField(
                            body,
                            propertyMembers.TakeAsArray(i + 1)),
                        nullExpression));
            }

            return Expression.AndAlso(
                nullCheckExpression,
                target);
        }

        public static Expression NestedPropertyOrField(Expression expression, string[] pathPropertyMembers)
        {
            // if nothing nested => nothing to do
            if (pathPropertyMembers.Length == 1)
            {
                return Expression.PropertyOrField(
                    expression,
                    pathPropertyMembers.First());
            }

            // otherwise resolve from deepest to direct
            // Prop1.Prop2.Prop3 ==> Prop3 is deepest
            var deepestPropertyName = pathPropertyMembers.Last();

            return Expression.PropertyOrField(
                NestedPropertyOrField(
                    expression,
                    pathPropertyMembers.RemoveLast()),
                deepestPropertyName);
        }

        public static Func<Expression, Expression, Expression> GetComparatorExpressionBuilder<T>(
            string propertyName,
            Comparator comparator)
        {
            switch (comparator)
            {
                case Comparator.Equals:
                    return Expression.Equal;
                case Comparator.NotEquals:
                    return Expression.NotEqual;
                case Comparator.Contains:
                    return CustomExpressions.GetContainsExpression<T>(propertyName);
                case Comparator.ContainsIgnoreCase:
                    return CustomExpressions.ContainsIgnoreCaseExpression;
                case Comparator.StartsWith:
                    return CustomExpressions.StartsWithExpression;
                case Comparator.StartsWithIgnoreCase:
                    return CustomExpressions.StartsWithIgnoreCaseExpression;
                case Comparator.IsOneOf:
                    return CustomExpressions.IsOneOfExpression;
                default:
                    throw new Exception(
                        $"Comparator {comparator} is currently not supported by GetComparatorExpressionBuilder");
            }
        }

        /// <summary>
        ///     Workflow:
        ///     1. Get the Property expression
        ///     2. Get the value expression
        ///     3. Get the Comparator expression
        ///     4. Merge them into one expression
        ///     Simple:
        ///     e.g. $x.Name == (System.String)"A"
        ///     Complex:
        ///     e.g. <c>x.Versions != null &amp;&amp; x.Versions.Any(x =&gt; x.Name != null &amp;&amp; x.Name.StartsWith('xx'))</c>
        /// </summary>
        public static Expression GetQueryFilterExpression<T>(
            QueryFilter queryFilter,
            ParameterExpression parameterExpression)
        {
            // First check if the query filter has a complex property access
            if (queryFilter.HasComplexPropertyAccess)
            {
                // get the propertyPath to the complex property
                string[] complexTypeIdentifierSplitted = queryFilter.Property.Split('<');
                var pathToTheComplexProperty = complexTypeIdentifierSplitted.First().ToPascalCase(); // e.g. versions
                string[] complexTypeIdentifierEndSplitted = complexTypeIdentifierSplitted[1].Split('>');
                var complexPropertyKind = complexTypeIdentifierEndSplitted.First(); // e.g. ANY
                var complexPropertyExpression = GetPropertyExpression(
                    pathToTheComplexProperty,
                    parameterExpression);

                // ResolvePropertyType understands the dot separated path used here. Wemogy.Core's
                // ResolvePropertyTypeOfPropertyPath does not: it splits on '/' and drops the first
                // segment, so every dot path resolved to an empty property name and threw.
                var complexPropertyType =
                    ResolvePropertyType<T>(pathToTheComplexProperty); // will be a list for now
                var innerParameterExpressionType =
                    complexPropertyType.GenericTypeArguments.First(); // List<Version> ==> Version

                // build the inner parameter expression
                var innerParameterExpressionName = $"{parameterExpression.Name}1"; // x ==> x1   x1 ==> x11   ...
                var innerParameterExpression =
                    Expression.Parameter(
                        innerParameterExpressionType,
                        innerParameterExpressionName);

                // build the query filter for the inner parameter expression. Everything after the
                // kind and its '>' is the property path inside the collection item, e.g.
                // versions<ANY>name ==> name. Substring is taken from the original identifier,
                // because re-joining the split segments dropped the first character of the path.
                var innerQueryFilter = queryFilter.Clone();
                innerQueryFilter.Property = complexTypeIdentifierSplitted[1]
                    .Substring(complexPropertyKind.Length + 1);

                var innerExpression = typeof(QueryParametersExtensions)
                    .GetMethod(nameof(GetQueryFilterExpression))?.MakeGenericMethod(innerParameterExpressionType)
                    .Invoke(
                        null,
                        new object[] { innerQueryFilter, innerParameterExpression }) as Expression;

                if (innerExpression == null)
                {
                    throw Error.Failure(
                        "QueryFilterExpressionNotBuilt",
                        $"The filter of the complex property {pathToTheComplexProperty} could not be translated into an expression");
                }

                Expression predicateExpression = Expression.Lambda(
                    innerExpression,
                    innerParameterExpression);

                var complexExpression = GetComplexPropertyExpressionBuilder(
                    complexPropertyKind,
                    innerParameterExpressionType,
                    complexPropertyExpression,
                    predicateExpression);

                return AddPropertyNullCheckExpression(
                    pathToTheComplexProperty,
                    parameterExpression,
                    complexExpression);
            }

            var propertyName = queryFilter.Property.ToPascalCase();
            var propertyExpression = GetPropertyExpression(
                propertyName,
                parameterExpression);
            var valueExpression = GetValueExpression<T>(
                propertyName,
                queryFilter.Value,
                queryFilter.Comparator);
            var comparatorExpressionBuilder = GetComparatorExpressionBuilder<T>(
                propertyName,
                queryFilter.Comparator);

            var fullExpression = comparatorExpressionBuilder(
                propertyExpression,
                valueExpression);

            return AddPropertyNullCheckExpression(
                queryFilter.Property.ToPascalCase(),
                parameterExpression,
                fullExpression);
        }

        public static Expression GetComplexPropertyExpressionBuilder(
            string complexPropertyKind,
            Type complexPropertyType,
            Expression complexPropertyExpression,
            Expression predicateExpression)
        {
            switch (complexPropertyKind.ToLower())
            {
                case "any":
                    var anyInfo = typeof(Enumerable)
                        .GetMethods(BindingFlags.Static | BindingFlags.Public)
                        .First(m => m.Name == "Any" && m.GetParameters().Count() == 2);
                    anyInfo = anyInfo.MakeGenericMethod(complexPropertyType);

                    return CustomExpressions.FalseIfPropertyIsNull(
                        complexPropertyExpression,
                        Expression.Call(
                            anyInfo,
                            complexPropertyExpression,
                            predicateExpression));
                default:
                    throw new Exception($"The complex property kind {complexPropertyKind} is not supported!");
            }
        }

        public static Func<Expression, Expression, Expression> GetExpressionTreeNodeIdExpressionBuilder(
            int expressionTreeNodeId)
        {
            var expressionIndicator = expressionTreeNodeId % 10;
            switch (expressionIndicator)
            {
                case 0:
                    return Expression.AndAlso;
                case 1:
                    return Expression.OrElse;
                default:
                    throw new Exception(
                        $"The expression indicator {expressionIndicator} of expressionTreeNodeId {expressionTreeNodeId} is not supported");
            }
        }

        private static string UseIfNotNullOrWhiteSpace(string keyword, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            return $"{keyword} {value}";
        }

        private static QueryDefinition GetQueryDefinition(
            this Container container,
            string selectStatement,
            QueryParameters queryParameters,
            MappingMetadata mappingMetadata,
            IQueryable generalFilter,
            ILogger? logger)
        {
            queryParameters.EnsureCamelCase();
            var whereCondition = queryParameters.GetQueryDefinitionFilterCondition(mappingMetadata, logger);
            var sorting = queryParameters.GetQueryDefinitionSort();

            var whereStatement = UseIfNotNullOrWhiteSpace(
                "WHERE",
                whereCondition.QueryText);
            var orderStatement = UseIfNotNullOrWhiteSpace(
                "ORDER BY",
                sorting.QueryText);
            var limitStatement =
                queryParameters.Take.HasValue ? $"OFFSET 0 LIMIT {queryParameters.Take}" : string.Empty;
            var joinStatement = string.Empty;

            // Prepend generalFilter to where statement, if given
            if (generalFilter != null)
            {
                // convert the IQueryable LINQ expression to a SQL query
                var generalFilterSql = generalFilter.ToString();

                logger?.LogDebug("generalFilterSql");
                logger?.LogDebug(generalFilterSql);

                // check if the stringified IQueryable LINQ expression equals the container link, which happens, if the query is empty
                var containerLink = $"dbs/{container.Database.Id}/colls/{container.Id}";
                if (generalFilterSql == containerLink)
                {
                    generalFilterSql = string.Empty;
                }

                // extract JOIN condition. An unfiltered IQueryable has no SQL to extract it from,
                // and SplitOnFirstOccurrence returns an empty array for an empty string, so Last()
                // would throw "Sequence contains no elements".
                if (!string.IsNullOrWhiteSpace(generalFilterSql))
                {
                    var join = generalFilterSql.SplitOnFirstOccurrence("FROM root").Last()
                        .SplitOnLastOccurrence("WHERE")
                        .First().Trim();
                    join = join
                        .Replace(
                            "root[",
                            "c[")
                        .Replace(
                            "FROM root",
                            "FROM c");
                    join = join.Replace(
                        "\\\"",
                        "\"");
                    if (!string.IsNullOrWhiteSpace(join))
                    {
                        logger?.LogDebug("JOIN");
                        logger?.LogDebug(join);
                        joinStatement = join;
                        logger?.LogDebug($"Join statement: {joinStatement}");
                    }
                }

                if (!string.IsNullOrWhiteSpace(generalFilterSql))
                {
                    // extract the WHERE condition from the SQL query. The same extraction turns a
                    // patch condition into a filter predicate, so it lives in one place
                    generalFilterSql = CosmosLinqQueryExtensions.ExtractWhereFragment(generalFilterSql);
                }

                if (!string.IsNullOrWhiteSpace(generalFilterSql))
                {
                    if (string.IsNullOrWhiteSpace(whereStatement))
                    {
                        whereStatement = $"WHERE {generalFilterSql}";
                    }
                    else
                    {
                        whereStatement = whereStatement.Replace(
                            "WHERE",
                            $"WHERE {generalFilterSql} AND ");
                    }
                }
            }

            var queryText = $@"
                {selectStatement}
                FROM {container.Id} c
                {joinStatement}
                {whereStatement}
                {orderStatement}
                {limitStatement}";

            var queryDefinition = new QueryDefinition(queryText);
            whereCondition.MergeParameters(sorting);

            foreach (var parameter in whereCondition.Parameters)
            {
                queryDefinition = queryDefinition.WithParameter(
                    parameter.Key,
                    parameter.Value);
            }

            logger?.LogDebug("Query:");
            logger?.LogDebug(queryText);
            logger?.LogDebug(JsonConvert.SerializeObject(queryDefinition.GetQueryParameters()));

            return queryDefinition;
        }

        public static FeedIterator<T> GetItemQueryIterator<T, TId>(
            this Container container,
            QueryParameters queryParameters,
            MappingMetadata mappingMetadata,
            IQueryable<T> generalFilter,
            ILogger? logger)
            where T : class
        {
            var queryDefinition = container.GetQueryDefinition(
                "SELECT VALUE c",
                queryParameters,
                mappingMetadata,
                generalFilter,
                logger);

            return container.GetItemQueryIterator<T>(queryDefinition);
        }

        public static FeedIterator<JObject> GetCount(
            this Container container,
            QueryParameters queryParameters,
            MappingMetadata mappingMetadata,
            bool softDeleteEnabled,
            IQueryable generalFilter,
            ILogger? logger)
        {
            var queryDefinition = container.GetQueryDefinition(
                "SELECT COUNT(1)",
                queryParameters,
                mappingMetadata,
                generalFilter,
                logger);

            return container.GetItemQueryIterator<JObject>(queryDefinition);
        }

        private static QueryDefinitionFilterCondition GetQueryDefinitionFilterCondition(
            this QueryParameters queryParameters, MappingMetadata mappingMetadata, ILogger? logger)
        {
            var result = new QueryDefinitionFilterCondition();

            foreach (var filter in queryParameters.Filters)
            {
                string condition;
                var valueDeserialized = mappingMetadata.Deserialize(
                    filter.Property,
                    filter.Value);

                switch (filter.Comparator)
                {
                    case Comparator.Equals:
                        if (valueDeserialized == null)
                        {
                            condition =
                                $"(IS_DEFINED(c.{filter.Property}) = false OR IS_NULL(c.{filter.Property}) = true)";
                        }
                        else
                        {
                            condition = $"c.{filter.Property} = @paramHere";
                        }

                        break;
                    case Comparator.NotEquals:
                        if (valueDeserialized == null)
                        {
                            condition = $"(IS_DEFINED(c.{filter.Property}) AND IS_NULL(c.{filter.Property}) = false)";
                        }
                        else
                        {
                            condition = $"c.{filter.Property} != @paramHere";
                        }

                        break;
                    case Comparator.StartsWith:
                        condition = $"STARTSWITH(c.{filter.Property}, @paramHere, false)";
                        break;
                    case Comparator.StartsWithIgnoreCase:
                        condition = $"STARTSWITH(c.{filter.Property}, @paramHere, true)";
                        break;
                    case Comparator.IsEmpty:
                        condition = $"ARRAY_LENGTH(c.{filter.Property}) = 0";
                        break;
                    case Comparator.IsNotEmpty:
                        condition = $"ARRAY_LENGTH(c.{filter.Property}) > 0";
                        break;
                    case Comparator.IsOneOf:
                        var arr = mappingMetadata.Deserialize(
                            filter.Property,
                            filter.Value) as JArray;
                        if (arr == null)
                        {
                            logger?.LogError(
                                $"Comparator.IsOneOf failed for filter: {JsonConvert.SerializeObject(filter)}");
                            continue;
                        }

                        var isOneOfQueryDefinition = new QueryDefinitionFilterCondition();
                        foreach (var item in arr)
                        {
                            var json = JsonConvert.SerializeObject(item);
                            isOneOfQueryDefinition.Or(
                                $"c.{filter.Property} = @paramHere",
                                mappingMetadata.Deserialize(
                                    filter.Property,
                                    json),
                                true);
                        }

                        condition = isOneOfQueryDefinition.QueryText;
                        result.MergeParameters(isOneOfQueryDefinition);
                        result.And(
                            condition,
                            true);
                        continue;
                    case Comparator.Contains:
                        condition = $"ARRAY_CONTAINS(c.{filter.Property}, @paramHere)";

                        // ToDo: remove next two lines and fix todo in 203
                        result.Or(
                            condition,
                            valueDeserialized,
                            true);
                        continue;
                    default:
                        logger?.LogError(
                            $"GetQueryDefinitionFilterCondition failed for filter: {JsonConvert.SerializeObject(filter)}");
                        continue;
                }

                // ToDo: support OR conditions (build the correct expression tree)
                result.And(
                    condition,
                    valueDeserialized);
            }

            var sortingQueryDefinition = new QueryDefinitionFilterCondition();

            // Only the leading sortings that carry a cursor take part in it.
            var searchAfterSortings = queryParameters.Sortings
                .TakeWhile(x => x.ContainsSearchAfter)
                .ToList();

            // c.Name > "A"
            // OR (c.Name = "A" AND c.createdAt > DT)
            // OR (c.Name = "A" AND c.createdAt = DT AND c.id > ID)
            for (var i = 0; i < searchAfterSortings.Count; i++)
            {
                var term = new QueryDefinitionFilterCondition();

                // every preceding column has to be equal for this term to decide
                for (var j = 0; j < i; j++)
                {
                    AppendSearchAfterCondition(
                        term,
                        searchAfterSortings[j],
                        "=",
                        mappingMetadata);
                }

                // the cursor has to move in the direction the column is ordered in. Comparing with
                // ">" for a descending column returns the half of the result set the caller has
                // already paged through.
                AppendSearchAfterCondition(
                    term,
                    searchAfterSortings[i],
                    searchAfterSortings[i].IsAscending ? ">" : "<",
                    mappingMetadata);

                sortingQueryDefinition.Or(
                    term.QueryText,
                    true);
                sortingQueryDefinition.MergeParameters(term);
            }

            result.And(
                sortingQueryDefinition.QueryText,
                true);
            result.MergeParameters(sortingQueryDefinition);

            return result;
        }

        private static void AppendSearchAfterCondition(
            QueryDefinitionFilterCondition term,
            QuerySorting sorting,
            string comparisonOperator,
            MappingMetadata mappingMetadata)
        {
            term.And(
                $"c.{sorting.OrderBy} {comparisonOperator} @paramHere",
                mappingMetadata.Deserialize(
                    sorting.OrderBy,
                    sorting.SearchAfter!));
        }

        private static QueryDefinitionFilterCondition GetQueryDefinitionSort(this QueryParameters queryParameters)
        {
            var result = new QueryDefinitionFilterCondition();

            // ToDo: support composite index
            // The order by query does not have a corresponding composite index that it can be served from.
            // queryParameters.Sortings = queryParameters.Sortings.Take(1).ToList();

            foreach (var sorting in queryParameters.Sortings)
            {
                var sortingDirection = sorting.IsAscending ? "ASC" : "DESC";
                var orderByStatement = $"c.{sorting.OrderBy} {sortingDirection}";
                result.Comma(orderByStatement);
            }

            return result;
        }
    }
}
