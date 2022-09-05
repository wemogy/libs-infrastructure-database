namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface ISoftDeletable
{
    public bool Deleted { get; set; }
}
