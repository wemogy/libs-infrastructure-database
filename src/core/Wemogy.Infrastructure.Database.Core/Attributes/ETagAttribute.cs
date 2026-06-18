using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

/// <summary>
///     Marks a property as the entity's eTag, used for optimistic concurrency.
///     Providers that support it (e.g. Cosmos DB) populate this property from the
///     store's eTag on read and exclude it from the persisted document body on write.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ETagAttribute : Attribute
{
}
