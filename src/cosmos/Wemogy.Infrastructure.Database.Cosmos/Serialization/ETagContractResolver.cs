using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     Contract resolver that applies camelCase naming and two special rules for
    ///     properties marked with the <see cref="ETagAttribute"/>:
    ///     <list type="number">
    ///         <item>they are read from Cosmos' system <c>_etag</c> field</item>
    ///         <item>they are never serialized into the persisted document body</item>
    ///     </list>
    /// </summary>
    public class ETagContractResolver : DefaultContractResolver
    {
        public ETagContractResolver()
        {
            NamingStrategy = new CamelCaseNamingStrategy();
        }

        protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization memberSerialization)
        {
            var property = base.CreateProperty(member, memberSerialization);

            if (member.GetCustomAttribute<ETagAttribute>() != null)
            {
                // Rule 1: read Cosmos' system "_etag" field into this property
                property.PropertyName = "_etag";

                // Rule 2: never persist the eTag into the document body, otherwise queries
                // would deserialize a stale value and cause false 412s on later replaces
                property.ShouldSerialize = _ => false;
            }

            return property;
        }
    }
}
