using System.Collections.Generic;
using System.Net;
using Microsoft.Azure.Cosmos;

namespace Wemogy.Infrastructure.Database.Cosmos.UnitTests.Fakes;

/// <summary>
///     In-memory <see cref="FeedResponse{T}"/> that returns a fixed page of items.
/// </summary>
/// <typeparam name="T">Type of the returned items.</typeparam>
public class FakeFeedResponse<T> : FeedResponse<T>
{
    private readonly List<T> _items;

    public FakeFeedResponse(List<T> items)
    {
        _items = items;
    }

    public override string ContinuationToken => string.Empty;

    public override int Count => _items.Count;

    public override Headers Headers => new Headers();

    public override IEnumerable<T> Resource => _items;

    public override HttpStatusCode StatusCode => HttpStatusCode.OK;

    public override CosmosDiagnostics Diagnostics => null!;

    public override string IndexMetrics => string.Empty;

    public override IEnumerator<T> GetEnumerator()
    {
        return _items.GetEnumerator();
    }
}
