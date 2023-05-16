using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Wemogy.Infrastructure.Database.InMemory.Helpers;

namespace Wemogy.Infrastructure.Database.InMemory.Extensions
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
            var searchAfterValue = querySorting.SearchAfter == null
                ? null
                : JsonConvert.DeserializeObject(
                    querySorting.SearchAfter,
                    propertyType);

            MethodInfo? comparisonMethod = null;
            Expression searchAfterValueExpression = Expression.Constant(searchAfterValue);

            // CompareTo is not working for GUID, for that reason we handle the GUID as string in comparison
            if (propertyType == typeof(Guid))
            {
                comparisonMethod = typeof(string).GetMethod(
                    nameof(string.CompareTo),
                    new[] { typeof(string) });
                var guidToStringMethod = propertyType.GetMethod(
                    nameof(string.ToString),
                    Type.EmptyTypes)!;
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
                    Type.EmptyTypes)!;
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

            Expression searchExpr;
            if (comparisonMethod == null)
            {
                searchExpr = Expression.GreaterThan(
                    propertyExpression,
                    searchAfterValueExpression);
            }
            else
            {
                Expression callExpr = Expression.Call(
                    propertyExpression,
                    comparisonMethod,
                    searchAfterValueExpression);
                searchExpr = Expression.GreaterThan(
                    callExpr,
                    Expression.Constant(0));
            }

            var myLambda =
                Expression.Lambda<Func<T, bool>>(
                    searchExpr,
                    param);

            return myLambda;

            /*
                        var propertyName = querySorting.OrderBy.ToPascalCase();

                        // x =>
                        var param = Expression.Parameter(typeof(T), "x");

                        // x.PropertyNameA.PropertyNameB
                        var prop = GetPropertyExpression(propertyName, param);

                        var propertyType = ResolvePropertyType<T>(propertyName);

                        var searchAfterValue = JsonConvert.DeserializeObject(querySorting.SearchAfter, typeof(string));

                        Expression searchExpr = Expression.GreaterThanOrEqual(prop, Expression.Constant(searchAfterValue));


                        Expression<Func<T, bool>> myLambda =
                            Expression.Lambda<Func<T, bool>>(searchExpr, param);

                        return myLambda;*/
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

            if (!queryFilters.Any())
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
        ///     e.g. $x.Folder != null AND ($x.Folder).Parent != null AND target
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
        ///     e.g. x.Versions != null AND x.Versions.Any(x => x.Name != null AND x.Name.StartsWith('xx'))
        /// </summary>
        public static Expression GetQueryFilterExpression<T>(
            QueryFilter queryFilter,
            ParameterExpression parameterExpression)
        {
            // First check if the query filter has a complex property access
            if (queryFilter.HasComplexPropertyAccess)
            {
                // get the propertyPath to the complex property
                string[] complexTypeIdentifierSplit = queryFilter.Property.Split('<');
                var pathToTheComplexProperty = complexTypeIdentifierSplit.First().ToPascalCase(); // e.g. versions
                string[] complexTypeIdentifierEndSplit = complexTypeIdentifierSplit[1].Split('>');
                var complexPropertyKind = complexTypeIdentifierEndSplit.First(); // e.g. ANY
                var complexPropertyExpression = GetPropertyExpression(
                    pathToTheComplexProperty,
                    parameterExpression);
                var complexPropertyType =
                    typeof(T).ResolvePropertyTypeOfPropertyPath(pathToTheComplexProperty)!; // will be a list for now
                var innerParameterExpressionType =
                    complexPropertyType.GenericTypeArguments.First(); // List<Version> ==> Version

                // build the inner parameter expression
                var innerParameterExpressionName = $"{parameterExpression.Name}1"; // x ==> x1   x1 ==> x11   ...
                var innerParameterExpression =
                    Expression.Parameter(
                        innerParameterExpressionType,
                        innerParameterExpressionName);

                // build the query filter for the inner parameter expression
                var innerQueryFilter = queryFilter.Clone();
                innerQueryFilter.Property = complexTypeIdentifierEndSplit.Skip(1).Join(">").Substring(1);

                var innerExpression = (Expression)typeof(QueryParametersExtensions)
                    .GetMethod(nameof(GetQueryFilterExpression))?.MakeGenericMethod(innerParameterExpressionType)
                    .Invoke(
                        null,
                        new object[] { innerQueryFilter, innerParameterExpression })!;

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
    }
}
