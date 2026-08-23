namespace Wemogy.Infrastructure.Database.Core.Models;

/// <summary>
///     The kind of a single operation of a partial document update.
/// </summary>
public enum DatabasePatchOperationKind
{
    /// <summary>
    ///     Writes a value, creating the field if the document does not carry it yet.
    /// </summary>
    Set,

    /// <summary>
    ///     Adds a signed numeric value to the current value of the field.
    /// </summary>
    Increment
}
