using System.Collections.Generic;

namespace Bdaya.Abp.EventBus.PubSub;

/// <summary>
/// Configuration options for the Pub/Sub distributed event bus.
/// Similar to RabbitMQ/Azure event bus options, following ABP patterns.
/// </summary>
public class AbpPubSubEventBusOptions
{
    /// <summary>
    /// The name of the connection to use from AbpPubSubOptions.Connections.
    /// If not set, uses the "Default" connection.
    /// </summary>
    public string? ConnectionName { get; set; }

    /// <summary>
    /// The Topic ID for publishing events.
    /// Events are published to this topic.
    /// </summary>
    public string TopicId { get; set; } = default!;

    /// <summary>
    /// The Subscription ID for receiving events.
    /// This application subscribes to this subscription to receive events.
    /// </summary>
    public string SubscriptionId { get; set; } = default!;

    /// <summary>
    /// Number of messages to pull in a single batch.
    /// Default: 10.
    /// </summary>
    public int MaxMessages { get; set; } = 10;

    /// <summary>
    /// The acknowledgment deadline in seconds.
    /// Messages not acknowledged within this time will be redelivered.
    /// Default: 60 seconds.
    /// </summary>
    public int AckDeadlineSeconds { get; set; } = 60;

    /// <summary>
    /// Whether to automatically create the topic if it doesn't exist.
    /// Default: true.
    /// </summary>
    public bool AutoCreateTopic { get; set; } = true;

    /// <summary>
    /// Whether to automatically create the subscription if it doesn't exist.
    /// Default: true.
    /// </summary>
    public bool AutoCreateSubscription { get; set; } = true;

    /// <summary>
    /// Optional message attributes to include with all published messages.
    /// These are added to every published event.
    /// </summary>
    public Dictionary<string, string> DefaultMessageAttributes { get; set; } = new();

    /// <summary>
    /// Filter expression for the subscription.
    /// Uses Pub/Sub filter syntax. Example: "attributes.type = \"OrderCreated\""
    /// </summary>
    public string? SubscriptionFilter { get; set; }

    /// <summary>
    /// Maximum concurrent message handlers.
    /// Controls parallelism of event processing.
    /// Default: 1.
    /// </summary>
    public int MaxConcurrentHandlers { get; set; } = 1;

    /// <summary>
    /// Whether to enable message ordering.
    /// When enabled, messages with the same ordering key are delivered in order.
    /// Default: false.
    /// </summary>
    public bool EnableMessageOrdering { get; set; } = false;

    /// <summary>
    /// Dead letter topic ID for failed messages.
    /// If set, messages that fail processing will be moved to this topic.
    /// </summary>
    public string? DeadLetterTopicId { get; set; }

    /// <summary>
    /// Maximum delivery attempts before moving to dead letter topic.
    /// Only applicable if DeadLetterTopicId is set.
    /// Default: 5.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 5;

    /// <summary>
    /// Message retention duration for the subscription in days.
    /// Default: 7 days.
    /// </summary>
    public int MessageRetentionDays { get; set; } = 7;
}
