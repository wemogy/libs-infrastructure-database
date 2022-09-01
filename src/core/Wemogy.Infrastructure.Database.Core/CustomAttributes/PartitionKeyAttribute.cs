using System;

namespace Wemogy.Infrastructure.Database.Core.CustomAttributes
{
    [AttributeUsage(AttributeTargets.Property)]
    public class PartitionKeyAttribute : Attribute
    {
    }
}
