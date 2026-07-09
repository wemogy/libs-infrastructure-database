using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Shouldly;
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

    private static void NormalizeETagProperties<T>(T entity)
        where T : class
    {
        foreach (var property in typeof(T)
                     .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.GetCustomAttribute<ETagAttribute>() != null))
        {
            property.SetValue(entity, null);
        }
    }
}
