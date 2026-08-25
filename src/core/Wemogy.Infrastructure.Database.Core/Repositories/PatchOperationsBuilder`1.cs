using System;
using System.Collections.Generic;
using System.Linq;
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

    private static readonly HashSet<Type> IntegralTypes = new HashSet<Type>
    {
        typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
        typeof(long), typeof(ulong)
    };

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
            null);
    }

    /// <inheritdoc />
    public IPatchOperations<TEntity> Increment(Expression<Func<TEntity, long>> path, long value)
    {
        return Add(
            DatabasePatchOperationKind.Increment,
            path,
            value,
            typeof(long));
    }

    /// <inheritdoc />
    public IPatchOperations<TEntity> Increment(Expression<Func<TEntity, double>> path, double value)
    {
        return Add(
            DatabasePatchOperationKind.Increment,
            path,
            value,
            typeof(double));
    }

    /// <inheritdoc />
    public IPatchOperations<TEntity> Increment(Expression<Func<TEntity, decimal>> path, decimal value)
    {
        return Add(
            DatabasePatchOperationKind.Increment,
            path,
            value,
            typeof(decimal));
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

        // a snapshot: the callback of the caller receives this builder and could hold on to it and
        // keep adding operations. The Cosmos provider translates when an operation is added while
        // the in-memory provider reads the list when the batch executes, so a shared list would let
        // the two providers apply different patches
        return _operations.ToArray();
    }

    private static IReadOnlyList<MemberInfo> ResolvePath(LambdaExpression path, Type? incrementValueType)
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

        if (incrementValueType != null)
        {
            EnsureLastMemberIsIncrementable(
                members,
                pathDescription,
                incrementValueType);
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

        // either attribute declares the partition key, and a component of a hierarchical one is
        // no more patchable than a single-value key: moving a document between partitions is a
        // delete and a create, not an update
        if (member.GetCustomAttribute<PartitionKeyAttribute>() != null ||
            member.GetCustomAttribute<HierarchicalPartitionKeyAttribute>() != null)
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

    private static void EnsureLastMemberIsIncrementable(
        IReadOnlyList<MemberInfo> members,
        string pathDescription,
        Type incrementValueType)
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
            throw PatchError.PathNotSupported(
                pathDescription,
                "only a numeric member can be incremented");
        }

        var underlyingMemberType = Nullable.GetUnderlyingType(memberType) ?? memberType;
        var scale = FixedPointMetadata.GetScale(member);

        if (incrementValueType == typeof(decimal))
        {
            // the decimal overload exists for fixed-point members only: they are the ones the
            // document carries as a scaled integer, which is what makes the increment exact
            if (scale == null)
            {
                var reason = underlyingMemberType == typeof(decimal)
                    ? $"a decimal member can only be incremented when it is marked with [FixedPoint], which persists it as an exact scaled integer. Mark {member.Name} with [FixedPoint(Scale = ...)], keep it in a long of minor units, or read-modify-write it"
                    : $"a decimal value can only increment a decimal member marked with [FixedPoint]; increment {member.Name} by a value of its own type instead";

                throw PatchError.PathNotSupported(
                    pathDescription,
                    reason);
            }

            return;
        }

        // a fixed-point member holds the value multiplied by 10^Scale, so a raw whole number would
        // move it by that many units of 10^-Scale, and a raw double would write a value the member
        // cannot read back exactly. Reachable through an explicit cast in the path, which
        // UnwrapNumericConversion unwraps, so it has to be refused here
        if (scale != null)
        {
            throw PatchError.PathNotSupported(
                pathDescription,
                $"{member.Name} is marked with [FixedPoint] and is stored as a scaled integer; increment it by a decimal value instead");
        }

        // a decimal without the attribute is deliberately not incrementable: Cosmos DB increments a
        // field as a 64-bit integer or as a double, and narrowing a decimal to a double would
        // silently lose precision on values that are usually money
        if (underlyingMemberType == typeof(decimal))
        {
            throw PatchError.PathNotSupported(
                pathDescription,
                "a decimal member cannot be incremented, because the database increments a field as a 64-bit integer or as a double and narrowing a decimal to a double would lose precision. Mark it with [FixedPoint(Scale = ...)] to store it as an exact scaled integer, keep it in a long of minor units, or read-modify-write it");
        }

        // a fractional increment on an integral field is refused instead of silently disagreeing
        // between the providers: Cosmos DB would store a non-integral number in a field the entity
        // reads back as an int, while the in-memory provider rounds the result to the member type
        if (incrementValueType == typeof(double) && IntegralTypes.Contains(underlyingMemberType))
        {
            throw PatchError.PathNotSupported(
                pathDescription,
                $"an integral member cannot be incremented by a floating point value; increment {member.Name} by a whole number instead");
        }

        // and the other way around, which a path can only reach through an explicit cast like
        // x => (long)x.Score: Cosmos DB would add the whole number to the fractional value it
        // finds, while the in-memory provider would do integer arithmetic on it
        if (incrementValueType == typeof(long) && !IntegralTypes.Contains(underlyingMemberType))
        {
            throw PatchError.PathNotSupported(
                pathDescription,
                $"a floating point member cannot be incremented by a whole number through a cast; increment {member.Name} by a floating point value instead");
        }
    }

    private static bool IsNumeric(Type type)
    {
        return NumericTypes.Contains(Nullable.GetUnderlyingType(type) ?? type);
    }

    private static object? ToScaledValue(object? value, int scale, string pathDescription)
    {
        if (value == null)
        {
            return null;
        }

        if (value is not decimal decimalValue)
        {
            throw PatchError.PathNotSupported(
                pathDescription,
                "a member marked with [FixedPoint] can only carry a decimal value");
        }

        return FixedPointScale.ToScaled(
            decimalValue,
            scale,
            pathDescription);
    }

    private IPatchOperations<TEntity> Add<TValue>(
        DatabasePatchOperationKind kind,
        Expression<Func<TEntity, TValue>> path,
        object? value,
        Type? incrementValueType)
    {
        if (_operations.Count >= MaxOperationCount)
        {
            throw PatchError.OperationLimitExceeded(MaxOperationCount);
        }

        var members = ResolvePath(
            path,
            incrementValueType);
        var scale = FixedPointMetadata.GetScale(members[members.Count - 1]);

        if (scale != null)
        {
            // a fixed-point member is carried as the scaled integer the document holds, for a Set
            // and for an Increment alike - so the value is validated here, once, and neither
            // provider has to know how the member is encoded
            value = ToScaledValue(
                value,
                scale.Value,
                path.ToString());
        }
        else if (kind == DatabasePatchOperationKind.Set)
        {
            // a Set can write a whole object that carries fixed-point members of its own, which
            // the providers serialize themselves - validated here so both refuse the same value
            FixedPointMetadata.EnsureValuesAreValid(value);
        }

        _operations.Add(
            new DatabasePatchOperation(
                kind,
                members,
                value,
                scale));

        return this;
    }
}
