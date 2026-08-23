using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;

/// <summary>
///     In-memory <see cref="FeedIterator{T}"/> that hands out a predefined list of pages.
/// </summary>
/// <typeparam name="T">Type of the returned items.</typeparam>
public class FakeFeedIterator<T> : FeedIterator<T>
{
    private readonly List<List<T>> _pages;
    private int _nextPageIndex;

    public FakeFeedIterator(params List<T>[] pages)
    {
        _pages = new List<List<T>>(pages);
    }

    public override bool HasMoreResults => _nextPageIndex < _pages.Count;

    /// <summary>
    ///     Gets the cancellation tokens the iterator was called with, so tests can verify that
    ///     the token is passed through.
    /// </summary>
    public List<CancellationToken> ReceivedCancellationTokens { get; } = new List<CancellationToken>();

    public override Task<FeedResponse<T>> ReadNextAsync(CancellationToken cancellationToken = default)
    {
        ReceivedCancellationTokens.Add(cancellationToken);
        var page = _pages[_nextPageIndex];
        _nextPageIndex++;
        return Task.FromResult<FeedResponse<T>>(new FakeFeedResponse<T>(page));
    }
}
