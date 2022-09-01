using Wemogy.Infrastructure.Database.Core.Enums;

namespace Wemogy.Infrastructure.Database.Core.ValueObjects
{
    public class QuerySorting
    {
        public SortOrder SortOrder { get; set; }

        public string OrderBy { get; set; }

        public string? SearchAfter { get; set; }

        public bool IsAscending => SortOrder == SortOrder.Ascending;

        public bool ContainsSearchAfter => !string.IsNullOrWhiteSpace(SearchAfter);

        public QuerySorting()
        {
            OrderBy = string.Empty;
        }
    }
}
