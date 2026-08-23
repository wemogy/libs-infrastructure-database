using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Cosmos.Client;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Constants;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Client;

public class CosmosDatabaseClientPatchTests
{
    [Fact]
    public async Task PatchAsync_ShouldRefuseAClientThatCannotReportItsMemberNames()
    {
        // Arrange: a client the caller brought along, configured with the serializer of the SDK.
        // Nothing can tell how it names a member, and a guessed path would create or overwrite a
        // field of the document that the entity never reads back
        var cosmosClient = new CosmosClient(TestingConstants.ConnectionString);
        var client = new CosmosDatabaseClient<UserWithETag>(
            cosmosClient,
            new CosmosDatabaseClientOptions(
                TestingConstants.DatabaseName,
                "users"),
            null);

        // Act: refused before a request is sent, so the client stays usable for everything else
        var exception = await Should.ThrowAsync<UnexpectedErrorException>(
            () => client.PatchAsync(
                "an-id",
                "a-partition",
                p => p.Set(x => x.Firstname, "Patched"),
                null,
                CancellationToken.None));

        // Assert
        exception.Code.ShouldBe("PatchMemberNamesNotResolvable");
    }
}
