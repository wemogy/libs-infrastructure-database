using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Wemogy.Infrastructure.Database.Cosmos.Extensions;
using Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;
using Xunit;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Extensions;

public class FeedIteratorExtensionsTests
{
    [Fact]
    public async Task IterateAsync_ShouldInvokeTheCallbackForEveryItemOfEveryPage()
    {
        // Arrange
        var feedIterator = new FakeFeedIterator<string>(
            new List<string> { "a", "b" },
            new List<string> { "c" });
        var visited = new List<string>();

        // Act
        await feedIterator.IterateAsync(
            item =>
            {
                visited.Add(item);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert: paging must be transparent to the caller
        visited.ShouldBe(new List<string> { "a", "b", "c" });
        feedIterator.HasMoreResults.ShouldBeFalse();
    }

    [Fact]
    public async Task IterateAsync_ShouldNotInvokeTheCallbackForAnEmptyResult()
    {
        // Arrange
        var feedIterator = new FakeFeedIterator<string>();
        var visited = new List<string>();

        // Act
        await feedIterator.IterateAsync(
            item =>
            {
                visited.Add(item);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert
        visited.ShouldBeEmpty();
    }

    [Fact]
    public async Task IterateAsync_ShouldSkipEmptyPages()
    {
        // Arrange: Cosmos may return an empty page while still reporting more results
        var feedIterator = new FakeFeedIterator<string>(
            new List<string>(),
            new List<string> { "a" });
        var visited = new List<string>();

        // Act
        await feedIterator.IterateAsync(
            item =>
            {
                visited.Add(item);
                return Task.CompletedTask;
            },
            CancellationToken.None);

        // Assert
        visited.ShouldBe(new List<string> { "a" });
    }

    [Fact]
    public async Task IterateAsync_ShouldPassTheCancellationTokenToEveryRead()
    {
        // Arrange
        using var cancellationTokenSource = new CancellationTokenSource();
        var feedIterator = new FakeFeedIterator<string>(
            new List<string> { "a" },
            new List<string> { "b" });

        // Act
        await feedIterator.IterateAsync(
            _ => Task.CompletedTask,
            cancellationTokenSource.Token);

        // Assert
        feedIterator.ReceivedCancellationTokens.Count.ShouldBe(2);
        feedIterator.ReceivedCancellationTokens.ShouldAllBe(x => x == cancellationTokenSource.Token);
    }

    [Fact]
    public async Task IterateAsync_ShouldPropagateExceptionsOfTheCallback()
    {
        // Arrange
        var feedIterator = new FakeFeedIterator<string>(new List<string> { "a", "b" });
        var visited = new List<string>();

        // Act
        var exception = await Record.ExceptionAsync(
            () => feedIterator.IterateAsync(
                item =>
                {
                    visited.Add(item);
                    throw new System.InvalidOperationException("callback failed");
                },
                CancellationToken.None));

        // Assert: iteration must stop at the failing item instead of swallowing the error
        exception.ShouldBeOfType<System.InvalidOperationException>();
        visited.ShouldBe(new List<string> { "a" });
    }
}
