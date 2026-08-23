using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Wemogy.Infrastructure.Database.Core.Models;

/// <summary>
///     One operation of a partial document update, in a form both providers can consume: the
///     member chain the path was resolved to, and the value to apply. The Cosmos provider turns
///     the chain into a JSON pointer through its serializer, the in-memory provider walks it by
///     reflection - so the providers cannot disagree about which field an operation addresses.
/// </summary>
public class DatabasePatchOperation
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="DatabasePatchOperation"/> class.
    /// </summary>
    /// <param name="kind">The kind of the operation</param>
    /// <param name="path">The resolved member chain, outermost member first</param>
    /// <param name="value">The value to write or to add</param>
    public DatabasePatchOperation(DatabasePatchOperationKind kind, IReadOnlyList<MemberInfo> path, object? value)
    {
        Kind = kind;
        Path = path;
        Value = value;
    }

    /// <summary>
    ///     The kind of the operation.
    /// </summary>
    public DatabasePatchOperationKind Kind { get; }

    /// <summary>
    ///     The members the path was resolved to, outermost member first: <c>x => x.Inner.Value</c>
    ///     resolves to <c>[Inner, Value]</c>.
    /// </summary>
    public IReadOnlyList<MemberInfo> Path { get; }

    /// <summary>
    ///     The value to write for a <see cref="DatabasePatchOperationKind.Set"/>, or the signed
    ///     value to add for a <see cref="DatabasePatchOperationKind.Increment"/>. An increment
    ///     value is always a <see cref="long"/> or a <see cref="double"/>.
    /// </summary>
    public object? Value { get; }

    /// <summary>
    ///     The path as it reads in an error message, e.g. <c>Inner.Value</c>.
    /// </summary>
    public string PathDescription => string.Join(".", Path.Select(x => x.Name));
}
