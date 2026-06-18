using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Azure.Cosmos;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Wemogy.Infrastructure.Database.Core.Attributes;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     Newtonsoft.Json based Cosmos serializer that keeps the previous default behavior
    ///     (camelCase property names, null values omitted) and additionally applies the
    ///     <see cref="ETagAttribute"/> serialization rules via <see cref="ETagContractResolver"/>.
    ///     It derives from <see cref="CosmosLinqSerializer"/> so that LINQ queries translate
    ///     member names with the very same naming rules.
    /// </summary>
    public class CosmosEntitySerializer : CosmosLinqSerializer
    {
        private static readonly Encoding DefaultEncoding = new UTF8Encoding(false, true);

        private readonly CamelCaseNamingStrategy _namingStrategy;
        private readonly JsonSerializer _serializer;

        public CosmosEntitySerializer()
        {
            _namingStrategy = new CamelCaseNamingStrategy();
            _serializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                ContractResolver = new ETagContractResolver()
            });
        }

        public override T FromStream<T>(Stream stream)
        {
            // when the caller materializes the raw stream, hand it back undisposed
            if (typeof(Stream).IsAssignableFrom(typeof(T)))
            {
                return (T)(object)stream;
            }

            using (stream)
            {
                using var streamReader = new StreamReader(stream);
                using var jsonTextReader = new JsonTextReader(streamReader);
                return _serializer.Deserialize<T>(jsonTextReader)!;
            }
        }

        public override Stream ToStream<T>(T input)
        {
            var streamPayload = new MemoryStream();
            using (var streamWriter = new StreamWriter(streamPayload, DefaultEncoding, 1024, leaveOpen: true))
            using (var jsonTextWriter = new JsonTextWriter(streamWriter) { Formatting = Formatting.None })
            {
                _serializer.Serialize(jsonTextWriter, input);
                jsonTextWriter.Flush();
                streamWriter.Flush();
            }

            streamPayload.Position = 0;
            return streamPayload;
        }

        public override string SerializeMemberName(MemberInfo memberInfo)
        {
            // keep LINQ query translation in sync with the rules applied by the contract resolver
            if (memberInfo.GetCustomAttribute<ETagAttribute>() != null)
            {
                return "_etag";
            }

            var jsonProperty = memberInfo.GetCustomAttribute<JsonPropertyAttribute>();
            if (!string.IsNullOrEmpty(jsonProperty?.PropertyName))
            {
                return jsonProperty!.PropertyName!;
            }

            return _namingStrategy.GetPropertyName(memberInfo.Name, false);
        }
    }
}
