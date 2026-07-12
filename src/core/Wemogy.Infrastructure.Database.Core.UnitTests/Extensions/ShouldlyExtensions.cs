using System;
using System.Collections.Generic;
using Shouldly;
using Wemogy.Core.Extensions;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Extensions;

public static class ShouldlyExtensions
{
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
    /// <remarks>
    ///     This <c>List&lt;T&gt;</c> overload is required because C# overload resolution
    ///     prefers <see cref="ShouldBeEquivalentToIgnoringETag{T}(T, T)"/> with
    ///     <c>T = List&lt;TItem&gt;</c> over the
    ///     <see cref="ShouldBeEquivalentToIgnoringETag{T}(IList{T}, IList{T})"/> overload
    ///     when the argument's static type is <c>List&lt;TItem&gt;</c>, bypassing
    ///     per-element ETag normalization.
    /// </remarks>
    public static void ShouldBeEquivalentToIgnoringETag<T>(this List<T> actual, List<T> expected)
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

        var properties = typeof(T).GetPropertiesByCustomAttribute<ETagAttribute>();

        foreach (var property in properties)
        {
            if (!property.CanWrite)
            {
                continue;
            }

            // Only set null for reference types and Nullable<T> value types
            if (!property.PropertyType.IsValueType ||
                Nullable.GetUnderlyingType(property.PropertyType) != null)
            {
                property.SetValue(entity, null);
            }
        }
    }
}
