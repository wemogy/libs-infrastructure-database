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

    public static NotFoundErrorException EntityNotFound(string id, string partitionKey)
    {
        return Error.NotFound(
            "EntityNotFound",
            $"Entity with id {id} was not found in partition {partitionKey}");
    }

    public static PreconditionFailedErrorException UnexpectedMultipleResults()
    {
        return Error.PreconditionFailed(
            "UnexpectedMultipleResults",
            "Querying for a single result returned more than one");
    }
}
