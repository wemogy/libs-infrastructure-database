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
    ///     member accesses, keyed by the dot separated path, e.g. <c>Inner.Value</c>. The lookup
    ///     ignores case, because a query addresses a property by its camelCased name.
    ///     <para>
    ///         Every member is registered under its CLR name and, when a naming rule is given,
    ///         under the name it is serialized as as well - a member renamed with a
    ///         <c>[JsonProperty]</c> is addressed by the stored name in a query, and looking only
    ///         for the CLR name would miss it and compare an unscaled value against the scaled
    ///         document.
    ///     </para>
    /// </summary>
    /// <param name="type">The type to inspect</param>
    /// <param name="serializeMemberName">
    ///     How a member is named in the document, null when only the CLR names are of interest
    /// </param>
    /// <returns>The scale of every reachable fixed-point member, by path</returns>
    public static IReadOnlyDictionary<string, int> GetScalesByPath(
        Type type,
        Func<MemberInfo, string>? serializeMemberName = null)
    {
        var scalesByPath = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        CollectScalesByPath(
            type,
            string.Empty,
            string.Empty,
            serializeMemberName,
            scalesByPath,
            new HashSet<Type>());

        return scalesByPath;
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
        string memberPathPrefix,
        string serializedPathPrefix,
        Func<MemberInfo, string>? serializeMemberName,
        IDictionary<string, int> scalesByPath,
        HashSet<Type> visitedTypes)
    {
        if (IsLeafType(type) || GetElementType(type) != null || !visitedTypes.Add(type))
        {
            return;
        }

        foreach (var member in GetDataMembers(type))
        {
            var memberPath = Append(
                memberPathPrefix,
                member.Name);
            var serializedPath = serializeMemberName == null
                ? memberPath
                : Append(
                    serializedPathPrefix,
                    serializeMemberName(member));
            var scale = GetScale(member);

            if (scale != null)
            {
                scalesByPath[memberPath] = scale.Value;
                scalesByPath[serializedPath] = scale.Value;
                continue;
            }

            CollectScalesByPath(
                GetMemberType(member)!,
                memberPath,
                serializedPath,
                serializeMemberName,
                scalesByPath,
                visitedTypes);
        }

        visitedTypes.Remove(type);
    }

    private static string Append(string prefix, string segment)
    {
        return prefix.Length == 0 ? segment : $"{prefix}.{segment}";
    }

    private static void EnsureValuesAreValid(object value, string path, HashSet<object> visitedValues)
    {
        // reference-equality, so a graph that points back at an entity it came from terminates
        // instead of recursing forever
        if (!visitedValues.Add(value))
        {
            return;
        }

        // a dictionary is walked by its values, the way its element type resolves: the entries
        // themselves carry no members a serializer would write
        if (value is IDictionary dictionary)
        {
            foreach (DictionaryEntry entry in dictionary)
            {
                EnsureItemIsValid(
                    entry.Value,
                    $"{path}[{entry.Key}]",
                    visitedValues);
            }

            return;
        }

        if (value is IEnumerable enumerable and not string)
        {
            var index = 0;

            foreach (var item in enumerable)
            {
                EnsureItemIsValid(
                    item,
                    $"{path}[{index}]",
                    visitedValues);
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

    private static void EnsureItemIsValid(object? item, string path, HashSet<object> visitedValues)
    {
        if (item == null || !HasFixedPointMembers(item.GetType()))
        {
            return;
        }

        EnsureValuesAreValid(
            item,
            path,
            visitedValues);
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
    ///     <para>
    ///         A dictionary resolves to the type of its <em>values</em>, which is what a serializer
    ///         writes the members of. Its <c>KeyValuePair</c> element type would be treated as a
    ///         leaf and hide them, while the Cosmos serializer still scales them - and the two
    ///         providers would disagree about a value only one of them refuses.
    ///     </para>
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

        var valueType = GetInterface(
            type,
            typeof(IDictionary<,>));

        if (valueType != null)
        {
            return valueType.GenericTypeArguments[1];
        }

        return GetInterface(
            type,
            typeof(IEnumerable<>))
            ?.GenericTypeArguments[0];
    }

    private static Type? GetInterface(Type type, Type genericTypeDefinition)
    {
        return type
            .GetInterfaces()
            .Concat(new[] { type })
            .FirstOrDefault(x => x.IsGenericType && x.GetGenericTypeDefinition() == genericTypeDefinition);
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
