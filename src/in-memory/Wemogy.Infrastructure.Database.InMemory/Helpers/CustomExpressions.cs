using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.InMemory.Helpers
{
    public static class CustomExpressions
    {
        public static Expression FalseIfPropertyIsNull(Expression propertyExpression, Expression target)
        {
            var nullExpression = Expression.Constant(
                null,
                typeof(object));
            return Expression.AndAlso(
                Expression.NotEqual(
                    propertyExpression,
                    nullExpression),
                target);
        }

        public static Expression ContainsIgnoreCaseExpression(Expression propertyExpression, Expression valueExpression)
        {
            var startsWithMethodInfo = typeof(string).GetMethod(
                "Contains",
                new[] { typeof(string), typeof(StringComparison) });

            if (startsWithMethodInfo == null)
            {
                throw new Exception("StartsWith was not found in string");
            }

            var ignoreCaseExpression = Expression.Constant(
                StringComparison.InvariantCultureIgnoreCase,
                typeof(StringComparison));
            return FalseIfPropertyIsNull(
                propertyExpression,
                Expression.Call(
                    propertyExpression,
                    startsWithMethodInfo,
                    valueExpression,
                    ignoreCaseExpression));
        }

        public static Func<Expression, Expression, Expression> GetContainsExpression<T>(string propertyName)
        {
            var propertyType = typeof(T).ResolvePropertyTypeOfPropertyName(propertyName);

            if (propertyType == typeof(List<Guid>))
            {
                return ContainsExpressionGuidList;
            }

            if (propertyType == typeof(List<string>))
            {
                return ContainsExpressionStringList;
            }

            if (propertyType == typeof(string))
            {
                return ContainsExpressionString;
            }

            throw new Exception($"GetContainsExpression not supported for property {propertyName}");
        }

        public static Expression ContainsExpressionGuidList(Expression propertyExpression, Expression valueExpression)
        {
            var containsMethodInfo = typeof(List<Guid>).GetMethod(
                "Contains",
                new[] { typeof(Guid) });

            if (containsMethodInfo == null)
            {
                throw new Exception("Contains was not found in List<Guid>");
            }

            return FalseIfPropertyIsNull(
                propertyExpression,
                Expression.Call(
                    propertyExpression,
                    containsMethodInfo,
                    valueExpression));
        }

        public static Expression ContainsExpressionStringList(Expression propertyExpression, Expression valueExpression)
        {
            var containsMethodInfo = typeof(List<string>).GetMethod(
                "Contains",
                new[] { typeof(string) });

            if (containsMethodInfo == null)
            {
                throw new Exception("Contains was not found in List<string>");
            }

            return FalseIfPropertyIsNull(
                propertyExpression,
                Expression.Call(
                    propertyExpression,
                    containsMethodInfo,
                    valueExpression));
        }

        public static Expression ContainsExpressionString(Expression propertyExpression, Expression valueExpression)
        {
            var containsMethodInfo = typeof(string).GetMethod(
                "Contains",
                new[] { typeof(string) });

            if (containsMethodInfo == null)
            {
                throw new Exception("Contains was not found in string");
            }

            return FalseIfPropertyIsNull(
                propertyExpression,
                Expression.Call(
                    propertyExpression,
                    containsMethodInfo,
                    valueExpression));
        }

        public static Expression StartsWithExpression(Expression propertyExpression, Expression valueExpression)
        {
            var startsWithMethodInfo = typeof(string).GetMethod(
                "StartsWith",
                new[] { typeof(string) });

            if (startsWithMethodInfo == null)
            {
                throw new Exception("StartsWith was not found in string");
            }

            return FalseIfPropertyIsNull(
                propertyExpression,
                Expression.Call(
                    propertyExpression,
                    startsWithMethodInfo,
                    valueExpression));
        }

        public static Expression StartsWithIgnoreCaseExpression(
            Expression propertyExpression,
            Expression valueExpression)
        {
            var startsWithMethodInfo = typeof(string).GetMethod(
                "StartsWith",
                new[] { typeof(string), typeof(StringComparison) });

            if (startsWithMethodInfo == null)
            {
                throw new Exception("StartsWith was not found in string");
            }

            var ignoreCaseExpression = Expression.Constant(
                StringComparison.InvariantCultureIgnoreCase,
                typeof(StringComparison));
            return FalseIfPropertyIsNull(
                propertyExpression,
                Expression.Call(
                    propertyExpression,
                    startsWithMethodInfo,
                    valueExpression,
                    ignoreCaseExpression));
            /*var ignoreCaseExpression = Expression.Constant(true, typeof(bool));
            var cultureInfoExpression = Expression.Constant(CultureInfo.CurrentCulture, typeof(CultureInfo));

            return FalseIfPropertyIsNull(
                propertyExpression,
                Expression.Call(propertyExpression, startsWithMethodInfo, valueExpression, ignoreCaseExpression, cultureInfoExpression));*/
        }

        public static Expression IsOneOfExpression(Expression propertyExpression, Expression valueExpression)
        {
            var startsWithMethodInfo = typeof(List<>).MakeGenericType(typeof(Guid))
                .GetMethod(
                    "Contains",
                    new[] { typeof(Guid) });

            if (startsWithMethodInfo == null)
            {
                throw new Exception("StartsWith was not found in string");
            }

            return Expression.Call(
                valueExpression,
                startsWithMethodInfo,
                propertyExpression);
        }
    }
}
