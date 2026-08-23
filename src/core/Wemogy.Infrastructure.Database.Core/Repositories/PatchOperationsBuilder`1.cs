using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using Wemogy.Infrastructure.Database.Core.Abstractions;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Core.Repositories;

/// <summary>
///     Collects and validates the operations of a partial document update. Every provider builds
///     its patches through this class, so a path either resolves the same way for both providers
///     or is rejected for both, with the same error.
/// </summary>
/// <typeparam name="TEntity">The entity type the operations act on</typeparam>
public class PatchOperationsBuilder<TEntity> : IPatchOperations<TEntity>
    where TEntity : class
{
    /// <summary>
    ///     Cosmos DB caps a partial document update at 10 operations. The cap is enforced for
    ///     every provider, so a patch that passes its test against the in-memory provider is not
    ///     too large for Cosmos DB.
    /// </summary>
    public const int MaxOperationCount = 10;

    private static readonly HashSet<Type> NumericTypes = new HashSet<Type>
    {
        typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong), typeof(float), typeof(double), typeof(decimal)
    };

    private readonly List<DatabasePatchOperation> _operations = new List<DatabasePatchOperation>();

    /// <inheritdoc />
    public int OperationCount => _operations.Count;

    /// <summary>
    ///     Runs the callback of the caller against a new builder and returns the operations it
    ///     added.
    /// </summary>
    /// <param name="operations">The callback that adds the operations</param>
    /// <returns>The validated operations, in the order they were added</returns>
    public static IReadOnlyList<DatabasePatchOperation> Build(Action<IPatchOperations<TEntity>>? operations)
    {
        var builder = new PatchOperationsBuilder<TEntity>();
        operations?.Invoke(builder);
        return builder.Build();
    }

    /// <inheritdoc />
    public IPatchOperations<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> path, TValue value)
    {
        return Add(
            DatabasePatchOperationKind.Set,
            path,
            value,
            false);
    }

    /// <inheritdoc />
    public IPatchOperations<TEntity> Increment(Expression<Func<TEntity, long>> path, long value)
    {
        return Add(
            DatabasePatchOperationKind.Increment,
            path,
            value,
            true);
    }

    /// <inheritdoc />
    public IPatchOperations<TEntity> Increment(Expression<Func<TEntity, double>> path, double value)
    {
        return Add(
            DatabasePatchOperationKind.Increment,
            path,
            value,
            true);
    }

    /// <summary>
    ///     Returns the collected operations.
    /// </summary>
    /// <returns>The validated operations, in the order they were added</returns>
    public IReadOnlyList<DatabasePatchOperation> Build()
    {
        // an empty patch is an error instead of a no-op: unlike a transactional batch, which a
        // caller can reach by looping over an empty collection, a patch without operations is
        // always a mistake at the call site
        if (_operations.Count == 0)
        {
            throw PatchError.IsEmpty();
        }

        return _operations;
    }

    private static IReadOnlyList<MemberInfo> ResolvePath(LambdaExpression path, bool requireNumericMember)
    {
        var pathDescription = path.ToString();
        var members = new List<MemberInfo>();

        // the compiler wraps the body in a numeric conversion when the member is narrower than the
        // parameter of the overload, e.g. an int member on Increment(..., long). The conversion
        // does not change which field is addressed, so it is unwrapped instead of rejected - any
        // other cast is not
        var expression = UnwrapNumericConversion(path.Body);

        while (expression is MemberExpression memberExpression)
        {
            members.Add(memberExpression.Member);
            expression = memberExpression.Expression;
        }

        // anything that is not a chain of member accesses rooted in the lambda parameter - a
        // method call, an indexer, a static member, a cast - cannot be expressed as a JSON path
        if (members.Count == 0 || expression != path.Parameters[0])
        {
            throw PatchError.PathNotSupported(pathDescription);
        }

        members.Reverse();

        EnsurePathIsAllowed(
            members,
            pathDescription);
        EnsureLastMemberIsWritable(
            members,
            pathDescription);

        if (requireNumericMember)
        {
            EnsureLastMemberIsNumeric(
                members,
                pathDescription);
        }

        return members;
    }

    private static Expression UnwrapNumericConversion(Expression expression)
    {
        if (expression is not UnaryExpression unaryExpression ||
            (expression.NodeType != ExpressionType.Convert && expression.NodeType != ExpressionType.ConvertChecked))
        {
            return expression;
        }

        return IsNumeric(unaryExpression.Type) && IsNumeric(unaryExpression.Operand.Type)
            ? unaryExpression.Operand
            : expression;
    }

    private static void EnsurePathIsAllowed(IReadOnlyList<MemberInfo> members, string pathDescription)
    {
        // only the outermost member can be the id, the partition key or the eTag of the document;
        // a member of a nested object of the same name is a different field
        var member = members[0];

        if (member.GetCustomAttribute<IdAttribute>() != null)
        {
            throw PatchError.PathNotAllowed(
                pathDescription,
                "id");
        }

        if (member.GetCustomAttribute<PartitionKeyAttribute>() != null)
        {
            throw PatchError.PathNotAllowed(
                pathDescription,
                "partition key");
        }

        if (member.GetCustomAttribute<ETagAttribute>() != null)
        {
            throw PatchError.PathNotAllowed(
                pathDescription,
                "eTag");
        }
    }

    private static void EnsureLastMemberIsWritable(IReadOnlyList<MemberInfo> members, string pathDescription)
    {
        var member = members[members.Count - 1];

        var isWritable = member switch
        {
            PropertyInfo propertyInfo => propertyInfo.CanWrite,
            FieldInfo fieldInfo => !fieldInfo.IsInitOnly && !fieldInfo.IsLiteral,
            _ => false
        };

        // a computed member has no field in the document to patch, and the in-memory provider
        // could not write it back either
        if (!isWritable)
        {
            throw PatchError.PathNotSupported(pathDescription);
        }
    }

    private static void EnsureLastMemberIsNumeric(IReadOnlyList<MemberInfo> members, string pathDescription)
    {
        var member = members[members.Count - 1];
        var memberType = member switch
        {
            PropertyInfo propertyInfo => propertyInfo.PropertyType,
            FieldInfo fieldInfo => fieldInfo.FieldType,
            _ => null
        };

        if (memberType == null || !IsNumeric(memberType))
        {
            throw PatchError.PathNotSupported(pathDescription);
        }
    }

    private static bool IsNumeric(Type type)
    {
        return NumericTypes.Contains(Nullable.GetUnderlyingType(type) ?? type);
    }

    private IPatchOperations<TEntity> Add<TValue>(
        DatabasePatchOperationKind kind,
        Expression<Func<TEntity, TValue>> path,
        object? value,
        bool requireNumericMember)
    {
        if (_operations.Count >= MaxOperationCount)
        {
            throw PatchError.OperationLimitExceeded(MaxOperationCount);
        }

        _operations.Add(
            new DatabasePatchOperation(
                kind,
                ResolvePath(path, requireNumericMember),
                value));

        return this;
    }
}
