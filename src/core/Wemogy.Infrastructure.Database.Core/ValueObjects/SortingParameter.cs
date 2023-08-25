using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Wemogy.Core.Errors;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Enums;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

public class SortingParameter
{
    private static readonly MethodInfo OrderByMethod = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == "OrderBy" && m.GetParameters().Length == 2);

    private static readonly MethodInfo OrderByDescendingMethod = typeof(Enumerable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == "OrderByDescending" && m.GetParameters().Length == 2);

    private static readonly MethodInfo QueryableOrderByMethod = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == "OrderBy" && m.GetParameters().Length == 2);

    private static readonly MethodInfo QueryableOrderByDescendingMethod = typeof(Queryable)
        .GetMethods(BindingFlags.Public | BindingFlags.Static)
        .Single(m => m.Name == "OrderByDescending" && m.GetParameters().Length == 2);

    public LambdaExpression PropertyExpression { get; }

    private string? _property;

    public string Property
    {
        get
        {
            if (_property == null)
            {
                _property = GetPropertyPath(PropertyExpression.Body);
            }

            return _property;
        }
    }

    private string? _camelCaseProperty;

    public string CamelCaseProperty
    {
        get
        {
            if (_camelCaseProperty == null)
            {
                _camelCaseProperty = GetCamelCasePropertyPath(PropertyExpression.Body);
            }

            return _camelCaseProperty;
        }
    }

    public SortDirection Direction { get; set; }

    public SortingParameter(LambdaExpression propertyExpression, SortDirection direction = SortDirection.Ascending)
    {
        PropertyExpression = propertyExpression;
        Direction = direction;
    }

    public IOrderedEnumerable<TEntity> Apply<TEntity>(IEnumerable<TEntity> enumerable)
    {
        var orderByMethod = (Direction == SortDirection.Ascending ? OrderByMethod : OrderByDescendingMethod)
            .MakeGenericMethod(typeof(TEntity), PropertyExpression.ReturnType);
        var orderedEnumerable = orderByMethod.Invoke(
            null,
            new object[] { enumerable, PropertyExpression.Compile() }) as IOrderedEnumerable<TEntity>;

        if (orderedEnumerable == null)
        {
            throw Error.Unexpected(
                "InvalidExpression",
                $"Expression {PropertyExpression} is not a valid property expression");
        }

        return orderedEnumerable;
    }

    public IOrderedQueryable<TEntity> Apply<TEntity>(IQueryable<TEntity> queryable)
    {
        var orderByMethod = (Direction == SortDirection.Ascending ? QueryableOrderByMethod : QueryableOrderByDescendingMethod)
            .MakeGenericMethod(typeof(TEntity), PropertyExpression.ReturnType);
        var orderedEnumerable = orderByMethod.Invoke(
            null,
            new object[] { queryable, PropertyExpression }) as IOrderedQueryable<TEntity>;

        if (orderedEnumerable == null)
        {
            throw Error.Unexpected(
                "InvalidExpression",
                $"Expression {PropertyExpression} is not a valid property expression");
        }

        return orderedEnumerable;
    }

    private static string GetPropertyPath(Expression expression)
    {
        if (expression is MemberExpression memberExpression)
        {
            var parentPath = GetPropertyPath(memberExpression.Expression);
            if (!string.IsNullOrEmpty(parentPath))
            {
                return $"{parentPath}.{memberExpression.Member.Name}";
            }

            return memberExpression.Member.Name;
        }

        if (expression is ParameterExpression)
        {
            return string.Empty;
        }

        throw Error.Unexpected(
            "InvalidExpression",
            $"Expression {expression} is not a valid property expression");
    }

    private static string GetCamelCasePropertyPath(Expression expression)
    {
        if (expression is MemberExpression memberExpression)
        {
            var parentPath = GetCamelCasePropertyPath(memberExpression.Expression);
            if (!string.IsNullOrEmpty(parentPath))
            {
                return $"{parentPath}.{memberExpression.Member.Name.ToCamelCase()}";
            }

            return memberExpression.Member.Name.ToCamelCase();
        }

        if (expression is ParameterExpression)
        {
            return string.Empty;
        }

        throw Error.Unexpected(
            "InvalidExpression",
            $"Expression {expression} is not a valid property expression");
    }
}
