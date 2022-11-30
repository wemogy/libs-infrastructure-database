using Wemogy.Core.Errors;
using Wemogy.Core.Errors.Exceptions;

namespace Wemogy.Infrastructure.Database.Core.Errors;

public static partial class DatabaseError
{
    public static PreconditionFailedErrorException UnexpectedMultipleResults()
    {
        return Error.PreconditionFailed(
            "UnexpectedMultipleResults",
            "Querying for a single result returned more than one");
    }
}
