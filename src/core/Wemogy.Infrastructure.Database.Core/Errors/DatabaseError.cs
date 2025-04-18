using System;
using Wemogy.Core.Errors;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Errors;

public static class DatabaseError
{
    public static NotFoundErrorException EntityNotFound()
    {
        return Error.NotFound(
            "EntityNotFound",
            "Requested entity was not found");
    }

    public static NotFoundErrorException EntityNotFound(string id)
    {
        return Error.NotFound(
            "EntityNotFound",
            $"Entity with id {id} not found");
    }

    public static NotFoundErrorException EntityNotFound(string id, string partitionKey, string hint = "", Exception? innerException = null)
    {
        if (hint != string.Empty)
        {
            hint = $" ({hint})";
        }

        return Error.NotFound(
            "EntityNotFound",
            $"Entity with id {id} was not found in partition {partitionKey}{hint}",
            innerException);
    }
}
