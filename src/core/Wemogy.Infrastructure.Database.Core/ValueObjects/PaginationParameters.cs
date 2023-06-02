namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

public class PaginationParameters
{
    public int Skip { get; set; }

    public int Take { get; set; }

    public PaginationParameters(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }
}
