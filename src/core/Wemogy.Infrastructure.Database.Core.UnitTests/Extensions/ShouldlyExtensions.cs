using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shouldly;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;

public static class ShouldlyExtensions
{
    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ETagPropertiesCache = new();

    /// <summary>
    ///     Asserts that <paramref name="actual"/> is structurally equivalent to
    ///     <paramref name="expected"/>, ignoring any properties decorated with
    ///     <see cref="ETagAttribute"/> because those are assigned automatically by
    ///     the database and are not manually managed.
    /// </summary>
    public static void ShouldBeEquivalentToIgnoringETag<T>(this T actual, T expected)
        where T : class
    {
        NormalizeETagProperties(actual);
        NormalizeETagProperties(expected);
        actual.ShouldBeEquivalentTo(expected);
    }

    /// <summary>
    ///     Asserts that <paramref name="actual"/> list is structurally equivalent to
    ///     <paramref name="expected"/> list, ignoring any properties decorated with
    ///     <see cref="ETagAttribute"/> because those are assigned automatically by
    ///     the database and are not manually managed.
    /// </summary>
    public static void ShouldBeEquivalentToIgnoringETag<T>(this IList<T> actual, IList<T> expected)
        where T : class
    {
        foreach (var item in actual)
        {
            NormalizeETagProperties(item);
        }

        foreach (var item in expected)
        {
            NormalizeETagProperties(item);
        }

        actual.ShouldBeEquivalentTo(expected);
    }

    private static void NormalizeETagProperties<T>(T entity)
        where T : class
    {
        if (entity == null)
        {
            return;
        }

        var properties = ETagPropertiesCache.GetOrAdd(
            typeof(T),
            t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                  .Where(p => p.CanWrite && p.GetCustomAttribute<ETagAttribute>() != null)
                  .ToArray());

        foreach (var property in properties)
        {
            // Only set null for reference types and Nullable<T> value types
            if (!property.PropertyType.IsValueType ||
                Nullable.GetUnderlyingType(property.PropertyType) != null)
            {
                property.SetValue(entity, null);
            }
        }
    }
}
