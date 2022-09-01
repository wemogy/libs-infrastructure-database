using System;
using System.Collections.Generic;
using Wemogy.Core.Extensions;

namespace Wemogy.Infrastructure.Database.Cosmos.Models
{
    public class QueryDefinitionFilterCondition
    {
        public string QueryText { get; set; }

        public Dictionary<string, object> Parameters { get; set; }

        public bool HasFilter => this.QueryText.Length > 0;

        private readonly string _parametersNamespace;

        public QueryDefinitionFilterCondition()
        {
            this.QueryText = "";
            this.Parameters = new Dictionary<string, object>();
            // short GUID used here
            this._parametersNamespace = Convert.ToBase64String(Guid.NewGuid().ToByteArray()).OnlyAlphanumeric();
        }

        public void And(string andCondition, bool withBrackets = false)
        {
            this.AppendCondition(andCondition, "AND", withBrackets);
        }

        public void Or(string orCondition, bool withBrackets = false)
        {
            this.AppendCondition(orCondition, "OR", withBrackets);
        }

        public void Comma(string commaStatement, bool withBrackets = false)
        {
            this.AppendCondition(commaStatement, ",", withBrackets);
        }

        /// <summary>
        ///
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

            if (this.HasFilter)
            {
                this.QueryText += $"{conditionOperator} {condition} ";
            }
            else
            {
                this.QueryText = $"{condition} ";
            }
        }

        public void And<TParameter>(string andCondition, TParameter parameter, bool withBrackets = false)
        {
            this.AppendCondition(andCondition, parameter, "AND", withBrackets);
        }

        public void Or<TParameter>(string orCondition, TParameter parameter, bool withBrackets = false)
        {
            this.AppendCondition(orCondition, parameter, "OR", withBrackets);
        }

        private void AppendCondition<TParameter>(string condition, TParameter parameter, string conditionOperator, bool withBrackets)
        {
            var parameterName = $"@param{this._parametersNamespace}_{this.Parameters.Count}";
            var fullCondition = condition.Replace("@paramHere", parameterName);

            // dateTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ");
            this.Parameters.Add(parameterName, parameter);
            this.AppendCondition(fullCondition, conditionOperator, withBrackets);
        }

        public void ReplaceGreaterThanWithEquals()
        {
            this.QueryText = this.QueryText.Replace(">", "=");
        }

        public void MergeParameters(QueryDefinitionFilterCondition queryDefinitionFilterCondition)
        {
            foreach (var parameter in queryDefinitionFilterCondition.Parameters)
            {
                if (this.Parameters.ContainsKey(parameter.Key))
                {
                    continue;
                }
                this.Parameters.Add(parameter.Key, parameter.Value);
            }
        }
    }
}
