using System.Text.Json.Serialization.Metadata;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     Applies the two <see cref="ETagAttribute"/> rules to the contract System.Text.Json
    ///     builds for an entity:
    ///     <list type="number">
    ///         <item>the property is read from Cosmos' system <c>_etag</c> field</item>
    ///         <item>the property is never serialized into the persisted document body</item>
    ///     </list>
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
                if (property.AttributeProvider?.IsDefined(typeof(ETagAttribute), true) != true)
                {
                    continue;
                }

                // Rule 1: read Cosmos' system "_etag" field into this property
                property.Name = ETagFieldName;

                // Rule 2: never persist the eTag into the document body, otherwise queries
                // would deserialize a stale value and cause false 412s on later replaces
                property.ShouldSerialize = (_, _) => false;
            }
        }
    }
}
