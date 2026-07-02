namespace Wemogy.Infrastructure.Database.Outbox.UnitTests.Fakes;

public class TestOrderPayload
{
    public string OrderId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}
