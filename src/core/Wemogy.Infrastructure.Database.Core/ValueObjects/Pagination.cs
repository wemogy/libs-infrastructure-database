namespace Wemogy.Infrastructure.Database.Core.ValueObjects;

public class Pagination
{
    public int Skip { get; set; }

    public int Take { get; set; }

    public Pagination(int skip, int take)
    {
        Skip = skip;
        Take = take;
    }
}
