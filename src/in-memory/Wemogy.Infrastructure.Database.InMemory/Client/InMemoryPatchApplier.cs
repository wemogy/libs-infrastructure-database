using System;
using System.Collections.Generic;
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
            var owners = ResolveOwners(
                target,
                operation,
                id,
                partitionKey);
            var owner = owners[owners.Count - 1];
            var member = operation.Path[operation.Path.Count - 1];

            // a fixed-point member is carried as the scaled integer the Cosmos document holds, so
            // it is brought back to the decimal the entity reads before it is written or added
            var operationValue = operation.Scale.HasValue && operation.Value is long scaledValue
                ? FixedPointScale.FromScaled(
                    scaledValue,
                    operation.Scale.Value)
                : operation.Value;

            var value = operation.Kind == DatabasePatchOperationKind.Set
                ? operationValue
                : Increment(
                    GetValue(owner, member),
                    operationValue);

            SetValue(
                owner,
                member,
                value);

            // reflection hands out a boxed copy of a value type, so writing a member of one would
            // be lost without assigning the copy back to the member it came from. Walked from the
            // innermost owner outwards, so a chain of value types is written back completely
            for (var i = owners.Count - 1; i > 0; i--)
            {
                if (owners[i].GetType().IsValueType)
                {
                    SetValue(
                        owners[i - 1],
                        operation.Path[i - 1],
                        owners[i]);
                }
            }
        }

        /// <summary>
        ///     Walks the path and returns every object it passes through, the entity first and the
        ///     object that owns the last member of the path last, so <c>x => x.Inner.Value</c>
        ///     resolves to <c>[entity, Inner]</c>.
        /// </summary>
        private static List<object> ResolveOwners(
            object target,
            DatabasePatchOperation operation,
            string id,
            string partitionKey)
        {
            var owners = new List<object> { target };

            for (var i = 0; i < operation.Path.Count - 1; i++)
            {
                var value = GetValue(
                    owners[i],
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

                owners.Add(value);
            }

            return owners;
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

            // a fixed-point member is added in decimal, so the result is exactly what Cosmos DB
            // computes on the scaled integers and divides by the same factor on read
            if (incrementValue is decimal decimalIncrement)
            {
                return ToDecimal(currentValue) + decimalIncrement;
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

        private static decimal ToDecimal(object? value)
        {
            return value == null ? 0m : Convert.ToDecimal(value);
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
