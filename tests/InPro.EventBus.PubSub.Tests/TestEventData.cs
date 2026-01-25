using Volo.Abp.EventBus;

namespace Bdaya.Abp.EventBus.PubSub.Tests;

/// <summary>
/// Sample event transfer object for testing.
/// </summary>
[EventName("Test.OrderCreated")]
public class OrderCreatedEto
{
    public Guid OrderId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// Another sample ETO for testing multiple event types.
/// </summary>
[EventName("Test.OrderCompleted")]
public class OrderCompletedEto
{
    public Guid OrderId { get; set; }
    public DateTime CompletedAt { get; set; }
}
