using System;

namespace Wemogy.Infrastructure.Database.Core.Attributes;

[AttributeUsage(AttributeTargets.Property)]
public class PartitionKeyAttribute : Attribute
{
}
