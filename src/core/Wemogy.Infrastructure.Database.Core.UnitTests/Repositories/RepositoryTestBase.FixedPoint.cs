using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Core.Errors.Exceptions;
using Wemogy.Infrastructure.Database.Core.Enums;
using Wemogy.Infrastructure.Database.Core.UnitTests.Fakes.Entities;
using Wemogy.Infrastructure.Database.Core.ValueObjects;
using Xunit;

namespace Wemogy.Infrastructure.Database.Core.UnitTests.Repositories;

/// <summary>
///     Covers a decimal member marked with the <c>[FixedPoint]</c> attribute. Every test here runs
///     against both providers, which is the point of the suite: the Cosmos provider stores the
///     member as a scaled integer while the in-memory provider evaluates the predicate against the
///     decimal the entity carries, and the two have to arrive at the same answer.
/// </summary>
public partial class RepositoryTestBase
{
    [Fact]
    public async Task CreateAsync_ShouldRoundTripAFixedPointDecimal()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 1234.567891m;
        user.Discount = 12.34m;

        // Act
        await MicrosoftUserRepository.CreateAsync(user);
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);

        // Assert: the value survives the scaled encoding unchanged
        persistedUser.Balance.ShouldBe(1234.567891m);
        persistedUser.Discount.ShouldBe(12.34m);
    }

    [Fact]
    public async Task CreateAsync_ShouldRefuseAValueFinerThanTheDeclaredScale()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 0.5000001m;

        // Act & Assert: refused by both providers, so a test against the in-memory one cannot
        // pass on a value Cosmos DB would have to round
        var exception = await Should.ThrowAsync<UnexpectedErrorException>(
            () => MicrosoftUserRepository.CreateAsync(user));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public async Task CreateAsync_ShouldRefuseAValueBeyondTheExactRange()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 10_000_000_000m;

        // Act & Assert: scaled by 10^6 this is past 2^53 - 1, where the database stops holding
        // integers exactly
        var exception = await Should.ThrowAsync<UnexpectedErrorException>(
            () => MicrosoftUserRepository.CreateAsync(user));
        exception.Code.ShouldBe("FixedPointValueOutOfRange");
    }

    [Fact]
    public async Task PatchAsync_ShouldIncrementAFixedPointDecimalExactly()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 0m;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act: ten increments of a value no binary floating point number can hold
        for (var i = 0; i < 10; i++)
        {
            await MicrosoftUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p => p.Increment(x => x.Balance, 0.1m));
        }

        // Assert: exactly 1, not 0.9999999999999999
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Balance.ShouldBe(1m);
    }

    [Fact]
    public async Task PatchAsync_ShouldDecrementAFixedPointDecimal()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 10.5m;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var patchedUser = await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Increment(x => x.Balance, -0.75m));

        // Assert
        patchedUser.Balance.ShouldBe(9.75m);
    }

    [Fact]
    public async Task PatchAsync_ShouldSetAFixedPointDecimal()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act
        await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p
                .Set(x => x.Balance, 42.000001m)
                .Set(x => x.Discount, 5.25m));

        // Assert: a Set writes the same encoding an increment adds to
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Balance.ShouldBe(42.000001m);
        persistedUser.Discount.ShouldBe(5.25m);
    }

    [Fact]
    public async Task PatchAsync_ShouldRefuseAnIncrementFinerThanTheDeclaredScale()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = await MicrosoftUserRepository.CreateAsync(NewUser(partitionKey));

        // Act & Assert: a hard error instead of a silent truncation of the seventh decimal place
        var exception = await Should.ThrowAsync<UnexpectedErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p => p.Increment(x => x.Balance, 0.0000001m)));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }

    [Fact]
    public async Task PatchAsync_ShouldApplyAConditionAgainstTheStoredScale()
    {
        // Arrange: the case #160 was opened for - a metered quota under a cap
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 99.5m;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act: the cap is a domain value, the field holds it multiplied by 10^6. A condition that
        // compared the two unscaled would refuse this patch
        var patchedUser = await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Increment(x => x.Balance, 0.5m),
            x => x.Balance <= 100m);

        // Assert
        patchedUser.Balance.ShouldBe(100m);
    }

    [Fact]
    public async Task PatchAsync_ShouldHoldAConditionExactlyAtTheBoundary()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 100m;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        var patchedUser = await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Increment(x => x.Balance, 1m),
            x => x.Balance <= 100m);

        // Assert: 100 is still inside the cap, and one more unit past it is not
        patchedUser.Balance.ShouldBe(101m);

        var exception = await Should.ThrowAsync<ConflictErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p => p.Increment(x => x.Balance, 1m),
                x => x.Balance <= 100m));
        exception.Code.ShouldBe("PatchConditionNotMet");

        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Balance.ShouldBe(101m);
    }

    [Fact]
    public async Task PatchAsync_ShouldRefuseAPatchWhoseConditionDoesNotHold()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 100.5m;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act & Assert
        var exception = await Should.ThrowAsync<ConflictErrorException>(
            () => MicrosoftUserRepository.PatchAsync(
                user.Id,
                partitionKey,
                p => p.Increment(x => x.Balance, 0.5m),
                x => x.Balance <= 100m));
        exception.Code.ShouldBe("PatchConditionNotMet");

        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Balance.ShouldBe(100.5m);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterOnAFixedPointDecimal()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var smallUser = NewUser(partitionKey);
        smallUser.Balance = 0.5m;
        var largeUser = NewUser(partitionKey);
        largeUser.Balance = 250.75m;
        await MicrosoftUserRepository.CreateAsync(smallUser);
        await MicrosoftUserRepository.CreateAsync(largeUser);

        // Act
        var underOneHundred = await MicrosoftUserRepository.QueryAsync(x => x.Balance <= 100m);
        var atLeastOne = await MicrosoftUserRepository.QueryAsync(x => x.Balance >= 1m);

        // Assert: an ordinary query predicate has to scale the same way a patch condition does
        underOneHundred.Select(x => x.Id).ShouldBe(new[] { smallUser.Id });
        atLeastOne.Select(x => x.Id).ShouldBe(new[] { largeUser.Id });
    }

    [Fact]
    public async Task CountAsync_ShouldCountOnAFixedPointDecimal()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var smallUser = NewUser(partitionKey);
        smallUser.Balance = 0.5m;
        var largeUser = NewUser(partitionKey);
        largeUser.Balance = 250.75m;
        await MicrosoftUserRepository.CreateAsync(smallUser);
        await MicrosoftUserRepository.CreateAsync(largeUser);

        // Act
        var count = await MicrosoftUserRepository.CountAsync(x => x.Balance < 100m);

        // Assert
        count.ShouldBe(1);
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterOnAFixedPointDecimalThroughQueryParameters()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var matchingUser = NewUser(partitionKey);
        matchingUser.Balance = 12.5m;
        var otherUser = NewUser(partitionKey);
        otherUser.Balance = 99m;
        await MicrosoftUserRepository.CreateAsync(matchingUser);
        await MicrosoftUserRepository.CreateAsync(otherUser);

        var queryParameters = new QueryParameters
        {
            Filters =
            {
                new QueryFilter
                {
                    Property = nameof(User.Balance),
                    Value = "12.5",
                    Comparator = Comparator.Equals
                }
            }
        };

        // Act
        var queriedUsers = await MicrosoftUserRepository.QueryAsync(queryParameters);

        // Assert: the string based filter path has to scale its value as well, or it compares
        // 12.5 against the 12500000 the document carries
        queriedUsers.Select(x => x.Id).ShouldBe(new[] { matchingUser.Id });
    }

    [Fact]
    public async Task PatchAsync_ShouldClearANullableFixedPointDecimal()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Discount = 7.5m;
        await MicrosoftUserRepository.CreateAsync(user);

        // Act
        await MicrosoftUserRepository.PatchAsync(
            user.Id,
            partitionKey,
            p => p.Set(x => x.Discount, null));

        // Assert: null is null at every scale
        var persistedUser = await MicrosoftUserRepository.GetAsync(
            user.Id,
            partitionKey);
        persistedUser.Discount.ShouldBeNull();
    }

    [Fact]
    public async Task TransactionalBatch_ShouldRefuseAValueFinerThanTheDeclaredScale()
    {
        // Arrange
        await ResetAsync();
        var partitionKey = NewPartitionKey();
        var user = NewUser(partitionKey);
        user.Balance = 0.5000001m;

        // Act & Assert: recorded operations are validated the same way a standalone write is
        var exception = Should.Throw<UnexpectedErrorException>(
            () => MicrosoftUserRepository.CreateTransactionalBatch(partitionKey).Create(user));
        exception.Code.ShouldBe("FixedPointPrecisionExceeded");
    }
}
