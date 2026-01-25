# Bdaya.Abp.EventBus.PubSub

Google Cloud Pub/Sub integration for ABP Framework's distributed event bus.

## Overview

This package provides an implementation of ABP's `IDistributedEventBus` using Google Cloud Pub/Sub as the message broker. It follows the same patterns as the official ABP RabbitMQ and Azure Service Bus integrations.

## Installation

### Using ABP CLI

```bash
abp add-package Bdaya.Abp.EventBus.PubSub
```

### Manual Installation

1. Install the NuGet package:
```bash
dotnet add package Bdaya.Abp.EventBus.PubSub
```

2. Add the module dependency to your ABP module:
```csharp
[DependsOn(typeof(AbpEventBusPubSubModule))]
public class YourModule : AbpModule
{
    // ...
}
```

## Configuration

### appsettings.json

```json
{
  "PubSub": {
    "Connections": {
      "Default": {
        "ProjectId": "your-gcp-project-id",
        "CredentialsPath": "/path/to/service-account.json"
      }
    },
    "EventBus": {
      "TopicId": "your-events-topic",
      "SubscriptionId": "your-app-subscription",
      "AutoCreateTopic": true,
      "AutoCreateSubscription": true
    }
  }
}
```

### Configuration Options

#### Connection Options (`AbpPubSubOptions`)

| Property | Description |
|----------|-------------|
| `Connections` | Dictionary of named connection configurations |
| `Default` | Shortcut to access `Connections["Default"]` |

#### Connection Configuration (`PubSubConnectionConfiguration`)

| Property | Description |
|----------|-------------|
| `ProjectId` | Google Cloud Project ID (required) |
| `CredentialsPath` | Path to service account JSON file (optional, uses ADC if not set) |
| `EmulatorHost` | Pub/Sub emulator host for local development (e.g., `localhost:8085`) |

#### Event Bus Options (`AbpPubSubEventBusOptions`)

| Property | Default | Description |
|----------|---------|-------------|
| `ConnectionName` | `null` | Named connection to use (uses "Default" if not set) |
| `TopicId` | - | Topic ID for publishing events (required) |
| `SubscriptionId` | - | Subscription ID for receiving events (required) |
| `MaxMessages` | `10` | Number of messages to pull in a batch |
| `AckDeadlineSeconds` | `60` | Message acknowledgment deadline |
| `AutoCreateTopic` | `true` | Auto-create topic if it doesn't exist |
| `AutoCreateSubscription` | `true` | Auto-create subscription if it doesn't exist |
| `DefaultMessageAttributes` | `{}` | Attributes to add to all published messages |
| `SubscriptionFilter` | `null` | Pub/Sub filter expression |
| `MaxConcurrentHandlers` | `1` | Maximum concurrent message handlers |
| `EnableMessageOrdering` | `false` | Enable message ordering by key |
| `DeadLetterTopicId` | `null` | Dead letter topic for failed messages |
| `MaxDeliveryAttempts` | `5` | Max delivery attempts before dead letter |
| `MessageRetentionDays` | `7` | Message retention duration |

### Code Configuration

You can also configure options in code:

```csharp
public override void ConfigureServices(ServiceConfigurationContext context)
{
    Configure<AbpPubSubOptions>(options =>
    {
        options.Connections.Default.ProjectId = "my-project";
        options.Connections.Default.CredentialsPath = "/path/to/credentials.json";
    });

    Configure<AbpPubSubEventBusOptions>(options =>
    {
        options.TopicId = "distributed-events";
        options.SubscriptionId = "my-service-subscription";
        options.MaxConcurrentHandlers = 5;
    });
}
```

## Usage

### Publishing Events

```csharp
public class MyService : ITransientDependency
{
    private readonly IDistributedEventBus _distributedEventBus;

    public MyService(IDistributedEventBus distributedEventBus)
    {
        _distributedEventBus = distributedEventBus;
    }

    public async Task DoSomethingAsync()
    {
        await _distributedEventBus.PublishAsync(
            new OrderCreatedEto
            {
                OrderId = Guid.NewGuid(),
                Amount = 100.00m
            }
        );
    }
}
```

### Subscribing to Events

```csharp
public class OrderCreatedHandler : IDistributedEventHandler<OrderCreatedEto>, ITransientDependency
{
    public async Task HandleEventAsync(OrderCreatedEto eventData)
    {
        // Handle the event
        Console.WriteLine($"Order created: {eventData.OrderId}");
    }
}
```

### Event Transfer Object (ETO)

```csharp
[EventName("Order.Created")]
public class OrderCreatedEto
{
    public Guid OrderId { get; set; }
    public decimal Amount { get; set; }
}
```

## Local Development with Emulator

For local development, you can use the Google Cloud Pub/Sub emulator:

1. Start the emulator:
```bash
gcloud beta emulators pubsub start --project=test-project
```

2. Configure your app to use the emulator:
```json
{
  "PubSub": {
    "Connections": {
      "Default": {
        "ProjectId": "test-project",
        "EmulatorHost": "localhost:8085"
      }
    },
    "EventBus": {
      "TopicId": "test-events",
      "SubscriptionId": "test-subscription"
    }
  }
}
```

## Authentication

The package supports three authentication methods:

1. **Application Default Credentials (ADC)** - Recommended for production
   - Uses `GOOGLE_APPLICATION_CREDENTIALS` environment variable
   - Automatically works with GKE Workload Identity
   - Works with Cloud Run, Cloud Functions, etc.

2. **Service Account JSON File**
   - Set `CredentialsPath` in connection configuration
   - Useful for local development or non-GCP environments

3. **Emulator**
   - Set `EmulatorHost` in connection configuration
   - No authentication required

## Multiple Connections

You can define multiple connections for different Pub/Sub projects:

```json
{
  "PubSub": {
    "Connections": {
      "Default": {
        "ProjectId": "project-1"
      },
      "Analytics": {
        "ProjectId": "analytics-project"
      }
    },
    "EventBus": {
      "ConnectionName": "Analytics",
      "TopicId": "analytics-events",
      "SubscriptionId": "my-app"
    }
  }
}
```

## Outbox/Inbox Pattern

This implementation supports ABP's outbox/inbox pattern for reliable event delivery. Configure your outbox/inbox as documented in the ABP Framework documentation.

## Comparison with Other Providers

| Feature | RabbitMQ | Azure Service Bus | Pub/Sub |
|---------|----------|-------------------|---------|
| Exchange/Topic | ✓ | ✓ | ✓ (Topic) |
| Queue/Subscription | ✓ | ✓ | ✓ (Subscription) |
| Dead Letter | ✓ | ✓ | ✓ |
| Message Ordering | Limited | ✓ | ✓ (with ordering key) |
| Filtering | Routing Key | SQL Filter | Filter Expression |
| Auto-scaling | Manual | ✓ | ✓ |

## License

This package is licensed under the same license as the parent project.
