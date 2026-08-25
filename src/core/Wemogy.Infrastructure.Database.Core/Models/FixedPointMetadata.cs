using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Errors;

namespace Wemogy.Infrastructure.Database.Core.Models;

/// <summary>
///     Reads the <see cref="FixedPointAttribute"/> off an entity type and validates the values an
///     entity carries against it. Every provider asks this class, so a member is scaled the same
///     way and an invalid value is refused by both of them.
///     <para>
///         The attribute is resolved by declared type, the way the Cosmos contract resolver
///         resolves it as well: a value reached through a member declared as <c>object</c> is not
///         inspected.
///     </para>
/// </summary>
public static class FixedPointMetadata
{
    private static readonly ConcurrentDictionary<MemberInfo, int?> Scales =
        new ConcurrentDictionary<MemberInfo, int?>();

    private static readonly ConcurrentDictionary<Type, bool> ContainsFixedPointMembers =
        new ConcurrentDictionary<Type, bool>();

    private static readonly ConcurrentDictionary<Type, MemberInfo[]> DataMembers =
        new ConcurrentDictionary<Type, MemberInfo[]>();

    private static readonly ConcurrentDictionary<Type, IReadOnlyDictionary<string, int>> ScalesByPath =
        new ConcurrentDictionary<Type, IReadOnlyDictionary<string, int>>();

    /// <summary>
    ///     Returns the declared scale of the member, or null when it is not a fixed-point member.
    /// </summary>
    /// <param name="member">The member to read the attribute off</param>
    /// <returns>The declared scale, or null</returns>
    public static int? GetScale(MemberInfo member)
    {
        return Scales.GetOrAdd(
            member,
            ResolveScale);
    }

    /// <summary>
    ///     Whether the type carries a fixed-point member, directly or through a nested member.
    ///     Used to skip the work of this class entirely for the entity types that do not use the
    ///     feature, which is most of them.
    /// </summary>
    /// <param name="type">The type to inspect</param>
    /// <returns>Whether anything in the type is a fixed-point member</returns>
    public static bool HasFixedPointMembers(Type type)
    {
        return ContainsFixedPointMembers.GetOrAdd(
            type,
            x => ContainsFixedPointMember(
                x,
                new HashSet<Type>()));
    }

    /// <summary>
    ///     Returns the scale of every fixed-point member reachable from the type by a chain of
    ///     member accesses, keyed by the dot separated member path, e.g. <c>Inner.Value</c>. The
    ///     lookup a caller builds from it has to ignore case, because a query addresses a property
    ///     by its serialized, camelCased name.
    /// </summary>
    /// <param name="type">The type to inspect</param>
    /// <returns>The scale of every reachable fixed-point member, by member path</returns>
    public static IReadOnlyDictionary<string, int> GetScalesByPath(Type type)
    {
        return ScalesByPath.GetOrAdd(
            type,
            x =>
            {
                var scalesByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                CollectScalesByPath(
                    x,
                    string.Empty,
                    scalesByPath,
                    new HashSet<Type>());
                return scalesByPath;
            });
    }

    /// <summary>
    ///     Throws when the entity carries a decimal that its declared scale cannot store exactly,
    ///     either because it has too many decimal places or because it is out of range. Called on
    ///     every write path of every provider, so a value the database would silently degrade is
    ///     refused before it is written - and refused by the in-memory provider as well, which
    ///     would otherwise accept a value its tests can never catch.
    /// </summary>
    /// <param name="entity">The entity to inspect, may be null</param>
    public static void EnsureValuesAreValid(object? entity)
    {
        if (entity == null || !HasFixedPointMembers(entity.GetType()))
        {
            return;
        }

        EnsureValuesAreValid(
            entity,
            entity.GetType().Name,
            new HashSet<object>(ReferenceEqualityComparer.Instance));
    }

    /// <summary>
    ///     Returns the type of a property or field, or null for any other kind of member.
    /// </summary>
    /// <param name="member">The member to read the type of</param>
    /// <returns>The declared type of the member, or null</returns>
    public static Type? GetMemberType(MemberInfo member)
    {
        return member switch
        {
            PropertyInfo propertyInfo => propertyInfo.PropertyType,
            FieldInfo fieldInfo => fieldInfo.FieldType,
            _ => null
        };
    }

    private static int? ResolveScale(MemberInfo member)
    {
        var attribute = member.GetCustomAttribute<FixedPointAttribute>();

        if (attribute == null)
        {
            return null;
        }

        var memberType = GetMemberType(member);
        var underlyingType = memberType == null ? null : Nullable.GetUnderlyingType(memberType) ?? memberType;

        if (underlyingType != typeof(decimal))
        {
            throw FixedPointError.NotADecimal(
                member,
                underlyingType?.Name ?? member.MemberType.ToString());
        }

        if (attribute.Scale < 0 || attribute.Scale > FixedPointScale.MaxScale)
        {
            throw FixedPointError.ScaleOutOfRange(
                member,
                attribute.Scale,
                FixedPointScale.MaxScale);
        }

        return attribute.Scale;
    }

    private static bool ContainsFixedPointMember(Type type, HashSet<Type> visitedTypes)
    {
        var elementType = GetElementType(type);

        if (elementType != null)
        {
            return ContainsFixedPointMember(
                elementType,
                visitedTypes);
        }

        if (IsLeafType(type) || !visitedTypes.Add(type))
        {
            return false;
        }

        return GetDataMembers(type)
            .Any(
                member => GetScale(member) != null ||
                    ContainsFixedPointMember(
                        GetMemberType(member)!,
                        visitedTypes));
    }

    private static void CollectScalesByPath(
        Type type,
        string pathPrefix,
        IDictionary<string, int> scalesByPath,
        HashSet<Type> visitedTypes)
    {
        if (IsLeafType(type) || GetElementType(type) != null || !visitedTypes.Add(type))
        {
            return;
        }

        foreach (var member in GetDataMembers(type))
        {
            var path = pathPrefix.Length == 0 ? member.Name : $"{pathPrefix}.{member.Name}";
            var scale = GetScale(member);

            if (scale != null)
            {
                scalesByPath[path] = scale.Value;
                continue;
            }

            CollectScalesByPath(
                GetMemberType(member)!,
                path,
                scalesByPath,
                visitedTypes);
        }

        visitedTypes.Remove(type);
    }

    private static void EnsureValuesAreValid(object value, string path, HashSet<object> visitedValues)
    {
        // reference-equality, so a graph that points back at an entity it came from terminates
        // instead of recursing forever
        if (!visitedValues.Add(value))
        {
            return;
        }

        if (value is IEnumerable enumerable and not string)
        {
            var index = 0;

            foreach (var item in enumerable)
            {
                if (item != null && HasFixedPointMembers(item.GetType()))
                {
                    EnsureValuesAreValid(
                        item,
                        $"{path}[{index}]",
                        visitedValues);
                }

                index++;
            }

            return;
        }

        foreach (var member in GetDataMembers(value.GetType()))
        {
            var memberValue = GetValue(
                value,
                member);

            if (memberValue == null)
            {
                continue;
            }

            var memberPath = $"{path}.{member.Name}";
            var scale = GetScale(member);

            if (scale != null)
            {
                FixedPointScale.ToScaled(
                    (decimal)memberValue,
                    scale.Value,
                    memberPath);
                continue;
            }

            // pruned by declared type, so a member of an unrelated type is never even read
            if (HasFixedPointMembers(GetMemberType(member)!))
            {
                EnsureValuesAreValid(
                    memberValue,
                    memberPath,
                    visitedValues);
            }
        }
    }

    private static object? GetValue(object owner, MemberInfo member)
    {
        return member switch
        {
            PropertyInfo propertyInfo => propertyInfo.GetValue(owner),
            FieldInfo fieldInfo => fieldInfo.GetValue(owner),
            _ => null
        };
    }

    private static MemberInfo[] GetDataMembers(Type type)
    {
        return DataMembers.GetOrAdd(
            type,
            x => x
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.CanRead && property.GetIndexParameters().Length == 0)
                .Cast<MemberInfo>()
                .Concat(x.GetFields(BindingFlags.Public | BindingFlags.Instance))
                .ToArray());
    }

    /// <summary>
    ///     The element type of a collection member, so a list of objects carrying a fixed-point
    ///     member is inspected as well. Null when the type is not a collection.
    /// </summary>
    private static Type? GetElementType(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return type
            .GetInterfaces()
            .Concat(new[] { type })
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            ?.GenericTypeArguments[0];
    }

    /// <summary>
    ///     Whether the type carries no members worth walking into. Everything the runtime library
    ///     offers is treated as a leaf, so the walk stays inside the entity types of the caller and
    ///     never reads a member of e.g. a <see cref="Type"/> or a stream.
    /// </summary>
    private static bool IsLeafType(Type type)
    {
        var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

        return underlyingType.IsPrimitive ||
            underlyingType.IsEnum ||
            underlyingType.IsPointer ||
            underlyingType.Namespace == null ||
            underlyingType.Namespace == "System" ||
            underlyingType.Namespace.StartsWith("System.", StringComparison.Ordinal);
    }
}
