using System;
using System.Collections.Generic;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Cosmos.Models
{
    public class QueryDefinitionFilterCondition
    {
        private readonly string _parametersNamespace;

        public QueryDefinitionFilterCondition()
        {
            QueryText = string.Empty;
            Parameters = new Dictionary<string, object>();

            // short GUID used here
            _parametersNamespace = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).OnlyAlphanumeric();
        }

        public string QueryText { get; set; }

        public Dictionary<string, object> Parameters { get; set; }

        public bool HasFilter => QueryText.Length > 0;

        public void And(string andCondition, bool withBrackets = false)
        {
            AppendCondition(
                andCondition,
                "AND",
                withBrackets);
        }

        public void Or(string orCondition, bool withBrackets = false)
        {
            AppendCondition(
                orCondition,
                "OR",
                withBrackets);
        }

        public void Comma(string commaStatement, bool withBrackets = false)
        {
            AppendCondition(
                commaStatement,
                ",",
                withBrackets);
        }

        /// <summary>
        /// </summary>
        /// <param name="condition">c.Name = "Some name"</param>
        /// <param name="conditionOperator">AND or OR</param>
        /// <param name="withBrackets"></param>
        private void AppendCondition(string condition, string conditionOperator, bool withBrackets)
        {
            if (string.IsNullOrWhiteSpace(condition))
            {
                return;
            }

            if (withBrackets)
            {
                condition = $"({condition})";
            }

            if (HasFilter)
            {
                QueryText += $"{conditionOperator} {condition} ";
            }
            else
            {
                QueryText = $"{condition} ";
            }
        }

        public void And<TParameter>(string andCondition, TParameter parameter, bool withBrackets = false)
        {
            AppendCondition(
                andCondition,
                parameter,
                "AND",
                withBrackets);
        }

        public void Or<TParameter>(string orCondition, TParameter parameter, bool withBrackets = false)
        {
            AppendCondition(
                orCondition,
                parameter,
                "OR",
                withBrackets);
        }

        private void AppendCondition<TParameter>(string condition, TParameter parameter, string conditionOperator,
            bool withBrackets)
        {
            var parameterName = $"@param{_parametersNamespace}_{Parameters.Count}";
            var fullCondition = condition.Replace(
                "@paramHere",
                parameterName);

            // dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            Parameters.Add(
                parameterName,
                parameter);
            AppendCondition(
                fullCondition,
                conditionOperator,
                withBrackets);
        }

        /// <summary>
        ///     Turns every "greater than" of the accumulated condition into an equality.
        /// </summary>
        /// <remarks>
        ///     Superseded by <see cref="ReplaceComparisonsWithEquals"/>, which also covers "less
        ///     than" and is therefore the right choice for a chain whose columns can be sorted in
        ///     different directions. Kept for compatibility.
        /// </remarks>
        public void ReplaceGreaterThanWithEquals()
        {
            QueryText = QueryText.Replace(
                ">",
                "=");
        }

        /// <summary>
        ///     Turns every comparison of the accumulated condition into an equality. This is how the
        ///     search-after chain builds its tie-breakers: <c>c.a &gt; A</c> becomes <c>c.a = A</c>
        ///     before the comparison of the next column is appended.
        ///     <para>
        ///         Both directions are rewritten, because the columns of one chain can be sorted
        ///         differently, e.g. <c>ORDER BY a ASC, b DESC</c>.
        ///     </para>
        /// </summary>
        public void ReplaceComparisonsWithEquals()
        {
            QueryText = QueryText
                .Replace(
                    ">",
                    "=")
                .Replace(
                    "<",
                    "=");
        }

        public void MergeParameters(QueryDefinitionFilterCondition queryDefinitionFilterCondition)
        {
            foreach (var parameter in queryDefinitionFilterCondition.Parameters)
            {
                if (Parameters.ContainsKey(parameter.Key))
                {
                    continue;
                }

                Parameters.Add(
                    parameter.Key,
                    parameter.Value);
            }
        }
    }
}
