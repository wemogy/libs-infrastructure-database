using Wemogy.Infrastructure.Database.Core.Enums;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects
{
    public class QueryFilter
    {
        public string Property { get; set; }

        public string Value { get; set; }

        public Comparator Comparator { get; set; }

        public int ExpressionTreeNodeId { get; set; }

        public int LevelId => (ExpressionTreeNodeId % 10000000) / 10000;
        public int GroupId => (ExpressionTreeNodeId % 10000) / 10;
        public int ParentGroupId => ExpressionTreeNodeId / 10000000;

        public bool HasComplexPropertyAccess => Property.Contains("<");
    }
}
