using System;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

public interface IEntityBase
{
    public string Id { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
