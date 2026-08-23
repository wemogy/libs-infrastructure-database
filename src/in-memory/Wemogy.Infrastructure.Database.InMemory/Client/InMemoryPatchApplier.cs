using System;
using System.Reflection;
using Wemogy.Infrastructure.Database.Core.Errors;
using Wemogy.Infrastructure.Database.Core.Models;

namespace Wemogy.Infrastructure.Database.InMemory.Client
{
    /// <summary>
    ///     Applies the operations of a partial document update to an entity by reflection, using
    ///     the member chain the core resolved the path to - so the in-memory provider writes the
    ///     very same field the Cosmos provider addresses with its JSON pointer.
    /// </summary>
    internal static class InMemoryPatchApplier
    {
        public static void Apply(
            object target,
            DatabasePatchOperation operation,
            string id,
            string partitionKey)
        {
            var owner = ResolveOwner(
                target,
                operation,
                id,
                partitionKey);
            var member = operation.Path[operation.Path.Count - 1];

            var value = operation.Kind == DatabasePatchOperationKind.Set
                ? operation.Value
                : Increment(
                    GetValue(owner, member),
                    operation.Value);

            SetValue(
                owner,
                member,
                value);
        }

        /// <summary>
        ///     Walks the path down to the object that owns its last member, so <c>x => x.Inner.Value</c>
        ///     resolves to the <c>Inner</c> instance.
        /// </summary>
        private static object ResolveOwner(
            object target,
            DatabasePatchOperation operation,
            string id,
            string partitionKey)
        {
            var owner = target;

            for (var i = 0; i < operation.Path.Count - 1; i++)
            {
                var value = GetValue(
                    owner,
                    operation.Path[i]);

                // Cosmos cannot patch a field of an object that is not there either, it answers
                // such a patch with a bad request
                if (value == null)
                {
                    throw PatchError.Failed(
                        id,
                        partitionKey,
                        $"the path {operation.PathDescription} passes through {operation.Path[i].Name}, which is null");
                }

                owner = value;
            }

            return owner;
        }

        /// <summary>
        ///     Adds the increment to the current value. A field the document does not carry starts
        ///     at zero, matching how Cosmos DB creates a missing field on an increment.
        /// </summary>
        private static object Increment(object? currentValue, object? incrementValue)
        {
            if (incrementValue is double doubleIncrement)
            {
                return ToDouble(currentValue) + doubleIncrement;
            }

            return ToLong(currentValue) + Convert.ToInt64(incrementValue);
        }

        private static long ToLong(object? value)
        {
            return value == null ? 0L : Convert.ToInt64(value);
        }

        private static double ToDouble(object? value)
        {
            return value == null ? 0d : Convert.ToDouble(value);
        }

        private static object? GetValue(object owner, MemberInfo member)
        {
            return member switch
            {
                PropertyInfo propertyInfo => propertyInfo.GetValue(owner),
                FieldInfo fieldInfo => fieldInfo.GetValue(owner),
                _ => null
            };
        }

        private static void SetValue(object owner, MemberInfo member, object? value)
        {
            switch (member)
            {
                case PropertyInfo propertyInfo:
                    propertyInfo.SetValue(
                        owner,
                        ConvertTo(value, propertyInfo.PropertyType));
                    break;
                case FieldInfo fieldInfo:
                    fieldInfo.SetValue(
                        owner,
                        ConvertTo(value, fieldInfo.FieldType));
                    break;
            }
        }

        /// <summary>
        ///     An increment is recorded as a long or a double, so the result has to be narrowed
        ///     back to the type of the member, e.g. to an int counter.
        /// </summary>
        private static object? ConvertTo(object? value, Type targetType)
        {
            if (value == null)
            {
                return null;
            }

            var type = Nullable.GetUnderlyingType(targetType) ?? targetType;

            return type.IsInstanceOfType(value)
                ? value
                : Convert.ChangeType(value, type);
        }
    }
}
