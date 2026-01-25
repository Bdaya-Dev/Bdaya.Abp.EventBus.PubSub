using System.Collections.Concurrent;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace Bdaya.Abp.EventBus.PubSub.Tests;

/// <summary>
/// Test handler that records received events for verification.
/// </summary>
public class TestOrderCreatedHandler : IDistributedEventHandler<OrderCreatedEto>, ITransientDependency
{
    public static ConcurrentBag<OrderCreatedEto> ReceivedEvents { get; } = new();
    public static TaskCompletionSource<OrderCreatedEto>? WaitHandle { get; set; }

    public Task HandleEventAsync(OrderCreatedEto eventData)
    {
        ReceivedEvents.Add(eventData);
        WaitHandle?.TrySetResult(eventData);
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        ReceivedEvents.Clear();
        WaitHandle = new TaskCompletionSource<OrderCreatedEto>();
    }
}

/// <summary>
/// Test handler for OrderCompletedEto.
/// </summary>
public class TestOrderCompletedHandler : IDistributedEventHandler<OrderCompletedEto>, ITransientDependency
{
    public static ConcurrentBag<OrderCompletedEto> ReceivedEvents { get; } = new();
    public static TaskCompletionSource<OrderCompletedEto>? WaitHandle { get; set; }

    public Task HandleEventAsync(OrderCompletedEto eventData)
    {
        ReceivedEvents.Add(eventData);
        WaitHandle?.TrySetResult(eventData);
        return Task.CompletedTask;
    }

    public static void Reset()
    {
        ReceivedEvents.Clear();
        WaitHandle = new TaskCompletionSource<OrderCompletedEto>();
    }
}
