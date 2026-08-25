using System;
using System.IO;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Azure.Cosmos;
using Wemogy.Infrastructure.Database.Core.Attributes;
using Wemogy.Infrastructure.Database.Core.Serialization;

namespace Wemogy.Infrastructure.Database.Cosmos.Serialization
{
    /// <summary>
    ///     System.Text.Json based Cosmos serializer that keeps the wire format the package has
    ///     always written (camelCase property names, null values omitted) and additionally applies
    ///     the <see cref="ETagAttribute"/> serialization rules.
    ///     It derives from <see cref="CosmosLinqSerializer"/> so that LINQ queries and patch paths
    ///     translate member names with the very same naming rules.
    /// </summary>
    /// <remarks>
    ///     The Cosmos SDK still uses Newtonsoft.Json for its own request and response types; the
    ///     serializer configured here is the one it applies to entities, query parameters and patch
    ///     values, which is everything this package puts on the wire.
    /// </remarks>
    public class CosmosEntitySerializer : CosmosLinqSerializer
    {
        private readonly JsonSerializerOptions _options;

        public CosmosEntitySerializer()
            : this(CreateDefaultOptions())
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="CosmosEntitySerializer"/> class from
        ///     custom options, for an entity that needs a converter of its own. Start from
        ///     <see cref="CreateDefaultOptions"/> and add to it, so the naming rules keep matching
        ///     what <see cref="SerializeMemberName"/> reports and the <see cref="ETagAttribute"/>
        ///     rules stay in place.
        /// </summary>
        /// <param name="options">The options to serialize an entity with.</param>
        public CosmosEntitySerializer(JsonSerializerOptions options)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        ///     The options this package configures Cosmos DB with.
        /// </summary>
        public static JsonSerializerOptions CreateDefaultOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,

                // Newtonsoft.Json matched a property name case insensitively, and a document
                // written by an older version of a consumer may well spell one differently
                PropertyNameCaseInsensitive = true,

                // the default encoder escapes every non-ASCII character; a Cosmos document is not
                // an HTML context, and the relaxed encoder keeps a name like "Müller" readable and
                // byte-identical to what Newtonsoft.Json wrote
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                TypeInfoResolver = new DefaultJsonTypeInfoResolver
                {
                    Modifiers = { EntityJsonTypeInfoModifier.Apply }
                }
            };

            options.Converters.Add(new UtcDateTimeOffsetJsonConverter());

            return options;
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
                return JsonSerializer.Deserialize<T>(
                    stream,
                    _options)!;
            }
        }

        public override Stream ToStream<T>(T input)
        {
            var streamPayload = new MemoryStream();

            // Onto the stream rather than through a writer of our own, because a hand-built
            // Utf8JsonWriter carries its own encoder and would quietly ignore the configured one.
            // By the runtime type rather than by T, because the SDK hands a query parameter over
            // as an object and serializing that by its declared type would write an empty document.
            JsonSerializer.Serialize(
                streamPayload,
                input,
                input?.GetType() ?? typeof(T),
                _options);

            streamPayload.Position = 0;
            return streamPayload;
        }

        public override string SerializeMemberName(MemberInfo memberInfo)
        {
            // keep LINQ query translation and patch paths in sync with the rules the contract
            // applies when the document is written
            if (memberInfo.GetCustomAttribute<ETagAttribute>() != null)
            {
                return EntityJsonTypeInfoModifier.ETagFieldName;
            }

            var nameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>();
            if (!string.IsNullOrEmpty(nameAttribute?.Name))
            {
                return nameAttribute!.Name;
            }

            return _options.PropertyNamingPolicy?.ConvertName(memberInfo.Name) ?? memberInfo.Name;
        }
    }
}
