using System;
using System.Reflection;
using System.Text.Json.Serialization.Metadata;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     Applies the library's own attributes to the contract System.Text.Json builds for an
    ///     entity: the two <see cref="ETagAttribute"/> rules
    ///     <list type="number">
    ///         <item>the property is read from Cosmos' system <c>_etag</c> field</item>
    ///         <item>the property is never serialized into the persisted document body</item>
    ///     </list>
    ///     and the scaling a <see cref="FixedPointAttribute"/> asks for.
    /// </summary>
    /// <remarks>
    ///     A contract modifier rather than a <c>[JsonPropertyName]</c> attribute, because the rules
    ///     hang off the library's own <see cref="ETagAttribute"/>: an entity marks its eTag once
    ///     and stays free of any serializer specific attribute.
    /// </remarks>
    internal static class EntityJsonTypeInfoModifier
    {
        public const string ETagFieldName = "_etag";

        public static void Apply(JsonTypeInfo typeInfo)
        {
            if (typeInfo.Kind != JsonTypeInfoKind.Object)
            {
                return;
            }

            foreach (var property in typeInfo.Properties)
            {
                if (property.AttributeProvider is not MemberInfo member)
                {
                    continue;
                }

                if (member.IsDefined(typeof(ETagAttribute), true))
                {
                    // Rule 1: read Cosmos' system "_etag" field into this property
                    property.Name = ETagFieldName;

                    // Rule 2: never persist the eTag into the document body, otherwise queries
                    // would deserialize a stale value and cause false 412s on later replaces
                    property.ShouldSerialize = (_, _) => false;
                }

                ApplyFixedPoint(
                    property,
                    member);
            }
        }

        /// <summary>
        ///     Persists a fixed-point decimal as the scaled integer it is exact as, so the
        ///     server-side increment of it is exact too.
        /// </summary>
        private static void ApplyFixedPoint(JsonPropertyInfo property, MemberInfo member)
        {
            var scale = FixedPointMetadata.GetScale(member);

            if (scale == null)
            {
                return;
            }

            var path = $"{member.DeclaringType?.Name}.{member.Name}";

            // a converter assigned to a property has to match that property's type exactly, so
            // the nullable member needs the converter written for a nullable decimal
            property.CustomConverter = Nullable.GetUnderlyingType(property.PropertyType) == typeof(decimal)
                ? new FixedPointNullableDecimalConverter(
                    scale.Value,
                    path)
                : new FixedPointDecimalConverter(
                    scale.Value,
                    path);
        }
    }
}
