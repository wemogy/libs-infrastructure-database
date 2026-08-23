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
using Wemogy.Infrastructure.Database.Cosmos.Serialization;

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
        ///     Used when the Cosmos client was configured with a serializer that cannot resolve
        ///     member names, so the names still follow the rules the rest of this library assumes.
        /// </summary>
        private static readonly CosmosEntitySerializer FallbackSerializer = new CosmosEntitySerializer();

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
        /// </summary>
        public static Func<MemberInfo, string> ResolveMemberNameSerializer(CosmosClient cosmosClient)
        {
            return cosmosClient.ClientOptions.Serializer is CosmosLinqSerializer linqSerializer
                ? linqSerializer.SerializeMemberName
                : FallbackSerializer.SerializeMemberName;
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
            return "/" + string.Join(
                "/",
                operation.Path.Select(serializeMemberName));
        }
    }
}
