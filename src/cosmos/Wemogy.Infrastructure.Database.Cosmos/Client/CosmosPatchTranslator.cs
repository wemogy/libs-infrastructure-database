using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;

namespace Wemogy.Infrastructure.Database.Cosmos.Client
{
    /// <summary>
    ///     Translates the provider-neutral patch operations and the patch condition into what the
    ///     Cosmos SDK expects: JSON pointers for the paths and a SQL filter predicate for the
    ///     condition.
    /// </summary>
    internal static class CosmosPatchTranslator
    {
        /// <summary>
        ///     Matches the <c>c["name"]</c> member access, including a nested <c>c["a"]["b"]</c>,
        ///     the Cosmos LINQ provider emits.
        /// </summary>
        private static readonly Regex BracketMemberAccess =
            new Regex("c(?:\\[\"(?<name>[^\"\\]]+)\"\\])+", RegexOptions.Compiled);

        private static readonly Regex Identifier = new Regex("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

        /// <summary>
        ///     Returns how the client of the given container names a member in the document, so a
        ///     patch path cannot disagree with what the serializer wrote.
        ///     <para>
        ///         A serializer that cannot report its member names leaves nothing to fall back on
        ///         but a guess, and a guessed path would create or overwrite the wrong field. The
        ///         returned delegate therefore throws instead - lazily, so a client configured that
        ///         way keeps working for everything but a patch.
        ///     </para>
        /// </summary>
        public static Func<MemberInfo, string> ResolveMemberNameSerializer(CosmosClient cosmosClient)
        {
            if (cosmosClient.ClientOptions.Serializer is CosmosLinqSerializer linqSerializer)
            {
                return linqSerializer.SerializeMemberName;
            }

            var serializerName = cosmosClient.ClientOptions.Serializer == null
                ? "the serializer of the SDK"
                : $"a {cosmosClient.ClientOptions.Serializer.GetType().Name}";

            return _ => throw PatchError.MemberNamesNotResolvable(serializerName);
        }

        /// <summary>
        ///     Whether the message of a bad request points at the filter predicate rather than at
        ///     the operations of the patch. A heuristic on the message of the provider, because the
        ///     status code carries the same value for both; when it does not match, the failure is
        ///     reported as a patch failure, which covers either cause.
        /// </summary>
        public static bool IsFilterPredicateFailure(string? message)
        {
            return message != null && message.Contains(
                "filter predicate",
                StringComparison.OrdinalIgnoreCase);
        }

        public static List<PatchOperation> ToPatchOperations(
            IReadOnlyList<DatabasePatchOperation> operations,
            Func<MemberInfo, string> serializeMemberName)
        {
            return operations
                .Select(
                    operation => ToPatchOperation(
                        operation,
                        serializeMemberName))
                .ToList();
        }

        /// <summary>
        ///     Turns the condition into the <c>FROM c WHERE ...</c> form the SDK expects for a
        ///     filter predicate, by letting the Cosmos LINQ provider translate it.
        /// </summary>
        public static string? ToFilterPredicate<TEntity>(
            Container container,
            Expression<Func<TEntity, bool>>? condition)
        {
            if (condition == null)
            {
                return null;
            }

            string? querySql;
            try
            {
                querySql = container
                    .GetItemLinqQueryable<TEntity>()
                    .Where(condition)
                    .ToString();
            }
            catch (Exception e)
            {
                // the LINQ provider refuses constructs it cannot express in SQL; surfacing that as
                // a patch error beats letting an opaque provider exception out
                throw PatchError.ConditionNotSupported(
                    condition.ToString(),
                    e.Message);
            }

            // a condition always produces a WHERE clause. If it did not, the predicate would be
            // dropped silently and the patch would apply unconditionally
            if (querySql == null || !querySql.Contains("WHERE"))
            {
                throw PatchError.ConditionNotSupported(condition.ToString());
            }

            var whereFragment = CosmosLinqQueryExtensions.ExtractWhereFragment(querySql);

            if (string.IsNullOrWhiteSpace(whereFragment))
            {
                throw PatchError.ConditionNotSupported(condition.ToString());
            }

            return $"FROM c WHERE {ToDotNotation(whereFragment!, condition)}";
        }

        /// <summary>
        ///     Rewrites the <c>c["name"]</c> member access the LINQ provider emits into the
        ///     <c>c.name</c> form a filter predicate accepts. The predicate is parsed by a stricter
        ///     parser than a query and rejects the bracket form with a bad request.
        /// </summary>
        private static string ToDotNotation<TEntity>(string whereFragment, Expression<Func<TEntity, bool>> condition)
        {
            return BracketMemberAccess.Replace(
                whereFragment,
                match =>
                {
                    var path = "c";

                    foreach (Capture capture in match.Groups["name"].Captures)
                    {
                        // a name that is not an identifier can only be addressed with brackets,
                        // which a filter predicate does not accept at all
                        if (!Identifier.IsMatch(capture.Value))
                        {
                            throw PatchError.ConditionNotSupported(
                                condition.ToString(),
                                $"the field {capture.Value} cannot be addressed in a filter predicate");
                        }

                        path += $".{capture.Value}";
                    }

                    return path;
                });
        }

        private static PatchOperation ToPatchOperation(
            DatabasePatchOperation operation,
            Func<MemberInfo, string> serializeMemberName)
        {
            var path = ToJsonPath(
                operation,
                serializeMemberName);

            if (operation.Kind == DatabasePatchOperationKind.Set)
            {
                return PatchOperation.Set(
                    path,
                    operation.Value);
            }

            // the builder only records a long or a double for an increment, matching the two
            // overloads the Cosmos SDK offers
            return operation.Value switch
            {
                long longValue => PatchOperation.Increment(path, longValue),
                double doubleValue => PatchOperation.Increment(path, doubleValue),
                _ => throw PatchError.PathNotSupported(operation.PathDescription)
            };
        }

        private static string ToJsonPath(
            DatabasePatchOperation operation,
            Func<MemberInfo, string> serializeMemberName)
        {
            var segments = operation.Path
                .Select(serializeMemberName)
                .ToList();

            foreach (var segment in segments)
            {
                EnsureSegmentIsAddressable(
                    segment,
                    operation);
            }

            return "/" + string.Join("/", segments);
        }

        /// <summary>
        ///     A patch path separates its segments with a <c>/</c>, and Cosmos DB does not unescape
        ///     one inside a segment: verified against the emulator, a <c>~1</c> escape is taken
        ///     literally and creates a field of that name, while the unescaped form steps into an
        ///     object that is not there. A field serialized under such a name is therefore refused
        ///     instead of written to the wrong place. The in-memory provider addresses members
        ///     directly and is not affected.
        /// </summary>
        private static void EnsureSegmentIsAddressable(string segment, DatabasePatchOperation operation)
        {
            if (!segment.Contains('/') && !segment.Contains('~'))
            {
                return;
            }

            throw PatchError.PathNotSupported(
                operation.PathDescription,
                $"the field is serialized as \"{segment}\", and Cosmos DB reads a / in a patch path as a step into a nested object and does not unescape a ~ - such a field cannot be patched");
        }
    }
}
