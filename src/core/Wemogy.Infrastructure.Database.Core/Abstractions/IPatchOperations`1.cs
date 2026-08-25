using System;
using System.Linq.Expressions;

namespace Wemogy.Infrastructure.Database.Core.Abstractions;

/// <summary>
///     The operations of a partial document update. Every operation addresses a field of the
///     document through a typed member expression, e.g. <c>x => x.Balance</c>.
///     <para>
///         A patch carries at most <see cref="Repositories.PatchOperationsBuilder{TEntity}.MaxOperationCount"/>
///         operations and has to carry at least one. The id, the partition key and the eTag of a
///         document cannot be patched.
///     </para>
/// </summary>
/// <typeparam name="TEntity">The entity type the operations act on</typeparam>
public interface IPatchOperations<TEntity>
{
    /// <summary>
    ///     The number of operations added so far.
    /// </summary>
    int OperationCount { get; }

    /// <summary>
    ///     Sets a field to the given value, creating it if the document does not carry it yet.
    ///     <para>
    ///         A field marked with the <see cref="Attributes.FixedPointAttribute"/> is written as
    ///         the scaled integer it is stored as, so a value with more decimal places than the
    ///         declared scale is refused instead of truncated.
    ///     </para>
    /// </summary>
    /// <param name="path">The field to set, e.g. <c>x => x.Firstname</c></param>
    /// <param name="value">The value to write</param>
    /// <typeparam name="TValue">The type of the field</typeparam>
    /// <returns>The same operations, so calls can be chained</returns>
    IPatchOperations<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> path, TValue value);

    /// <summary>
    ///     Adds the given value to a numeric field. The value is signed, so a decrement is an
    ///     increment by a negative value - there is no <c>Decrement</c>. A field the document does
    ///     not carry yet starts at zero.
    /// </summary>
    /// <param name="path">The field to increment, e.g. <c>x => x.Balance</c></param>
    /// <param name="value">The value to add, negative to subtract</param>
    /// <returns>The same operations, so calls can be chained</returns>
    IPatchOperations<TEntity> Increment(Expression<Func<TEntity, long>> path, long value);

    /// <summary>
    ///     Adds the given value to a numeric field. The value is signed, so a decrement is an
    ///     increment by a negative value - there is no <c>Decrement</c>. A field the document does
    ///     not carry yet starts at zero.
    /// </summary>
    /// <param name="path">The field to increment, e.g. <c>x => x.Score</c></param>
    /// <param name="value">The value to add, negative to subtract</param>
    /// <returns>The same operations, so calls can be chained</returns>
    IPatchOperations<TEntity> Increment(Expression<Func<TEntity, double>> path, double value);

    /// <summary>
    ///     Adds the given value to a <c>decimal</c> field marked with the
    ///     <see cref="Attributes.FixedPointAttribute"/>. The value is signed, so a decrement is an
    ///     increment by a negative value - there is no <c>Decrement</c>. A field the document does
    ///     not carry yet starts at zero.
    ///     <para>
    ///         The field is only incrementable because the attribute persists it as the integer
    ///         <c>value * 10^Scale</c>: Cosmos DB increments a field as a 64-bit integer or as a
    ///         double, and narrowing a decimal to a double would silently lose the base-10
    ///         exactness of values that are usually money. A decimal without the attribute is
    ///         therefore still refused, and so is a value with more decimal places than the
    ///         declared scale.
    ///     </para>
    /// </summary>
    /// <param name="path">The field to increment, e.g. <c>x => x.Balance</c></param>
    /// <param name="value">The value to add, negative to subtract</param>
    /// <returns>The same operations, so calls can be chained</returns>
    IPatchOperations<TEntity> Increment(Expression<Func<TEntity, decimal>> path, decimal value);
}
