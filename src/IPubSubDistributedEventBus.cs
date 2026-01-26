using Volo.Abp.EventBus.Distributed;

namespace Bdaya.Abp.EventBus.PubSub;

/// <summary>
/// Interface for the Pub/Sub distributed event bus.
/// Extends IDistributedEventBus with Pub/Sub-specific functionality.
/// </summary>
public interface IPubSubDistributedEventBus : IDistributedEventBus
{
    /// <summary>
    /// Initializes the Pub/Sub event bus asynchronously.
    /// Creates topic and subscription if needed, and starts listening for messages.
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Stops the Pub/Sub event bus.
    /// </summary>
    Task StopAsync();
}
