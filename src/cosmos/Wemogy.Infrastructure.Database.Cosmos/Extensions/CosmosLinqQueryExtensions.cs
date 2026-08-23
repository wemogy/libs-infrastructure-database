using System.Linq;

namespace Wemogy.Infrastructure.Database.Cosmos.Extensions
{
    /// <summary>
    ///     Helpers around the Cosmos LINQ provider.
    /// </summary>
    public static class CosmosLinqQueryExtensions
    {
        /// <summary>
        ///     Extracts the <c>WHERE</c> condition from the SQL the Cosmos LINQ provider produced
        ///     for a queryable, in the alias and quoting this library uses.
        ///     <para>
        ///         Letting the provider translate the expression is what keeps property naming
        ///         consistent with the naming the serializer applies - the alternative would be a
        ///         second, hand-rolled expression-to-SQL translation that drifts from it.
        ///     </para>
        /// </summary>
        /// <param name="querySql">
        ///     The result of <c>ToString()</c> on a queryable, which the provider returns as a JSON
        ///     document of the shape <c>{"query":"SELECT VALUE root FROM root WHERE ..."}</c>
        /// </param>
        /// <returns>The condition, or null if the SQL does not carry one</returns>
        public static string? ExtractWhereFragment(string? querySql)
        {
            if (string.IsNullOrWhiteSpace(querySql))
            {
                return null;
            }

            var fragment = querySql!.Split("WHERE").LastOrDefault();

            // the trailing quote and brace of the JSON document the provider returns
            if (fragment == null || fragment.Length < 2)
            {
                return null;
            }

            fragment = fragment
                .Remove(fragment.Length - 2)
                .Trim();

            // the provider emits the root alias, the queries of this library use c
            fragment = fragment.Replace(
                "root[",
                "c[");

            // the JSON document escapes the quotes around the property names
            return fragment.Replace(
                "\\\"",
                "\"");
        }
    }
}
