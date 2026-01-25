using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Tracing;
using Volo.Abp.Uow;

namespace Bdaya.Abp.EventBus.PubSub;

/// <summary>
/// Google Cloud Pub/Sub implementation of the ABP distributed event bus.
/// Provides publish/subscribe messaging for distributed events.
/// </summary>
[Dependency(ReplaceServices = true)]
[ExposeServices(typeof(IDistributedEventBus), typeof(PubSubDistributedEventBus), typeof(IPubSubDistributedEventBus))]
public class PubSubDistributedEventBus : DistributedEventBusBase, IPubSubDistributedEventBus, ISingletonDependency
{
    protected AbpPubSubEventBusOptions PubSubEventBusOptions { get; }
    protected IPubSubConnectionPool ConnectionPool { get; }
    protected IPubSubSerializer Serializer { get; }
    protected ILogger<PubSubDistributedEventBus> Logger { get; }

    protected ConcurrentDictionary<Type, List<IEventHandlerFactory>> HandlerFactories { get; }
    protected ConcurrentDictionary<string, Type> EventTypes { get; }

    private SubscriberClient? _subscriberClient;
    private CancellationTokenSource? _cancellationTokenSource;
    private TopicName? _topicName;
    private SubscriptionName? _subscriptionName;
    private bool _initialized;

    public PubSubDistributedEventBus(
        IOptions<AbpPubSubEventBusOptions> pubSubEventBusOptions,
        IPubSubConnectionPool connectionPool,
        IPubSubSerializer serializer,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AbpDistributedEventBusOptions> distributedEventBusOptions,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IGuidGenerator guidGenerator,
        IClock clock,
        IEventHandlerInvoker eventHandlerInvoker,
        ILocalEventBus localEventBus,
        ICorrelationIdProvider correlationIdProvider,
        ILogger<PubSubDistributedEventBus> logger)
        : base(
            serviceScopeFactory,
            currentTenant,
            unitOfWorkManager,
            distributedEventBusOptions,
            guidGenerator,
            clock,
            eventHandlerInvoker,
            localEventBus,
            correlationIdProvider)
    {
        PubSubEventBusOptions = pubSubEventBusOptions.Value;
        ConnectionPool = connectionPool;
        Serializer = serializer;
        Logger = logger;

        HandlerFactories = new ConcurrentDictionary<Type, List<IEventHandlerFactory>>();
        EventTypes = new ConcurrentDictionary<string, Type>();
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        var connection = ConnectionPool.GetConnection(PubSubEventBusOptions.ConnectionName);

        _topicName = TopicName.FromProjectTopic(connection.ProjectId, PubSubEventBusOptions.TopicId);
        _subscriptionName = SubscriptionName.FromProjectSubscription(connection.ProjectId, PubSubEventBusOptions.SubscriptionId);

        // Ensure topic and subscription exist
        await EnsureTopicExistsAsync();
        await EnsureSubscriptionExistsAsync();

        // Start the subscriber
        await StartSubscriberAsync();

        // Subscribe configured handlers
        SubscribeHandlers(AbpDistributedEventBusOptions.Handlers);

        _initialized = true;

        Logger.LogInformation(
            "Pub/Sub Event Bus initialized. Topic: {TopicName}, Subscription: {SubscriptionName}",
            _topicName.ToString(),
            _subscriptionName.ToString());
    }

    private async Task EnsureTopicExistsAsync()
    {
        if (!PubSubEventBusOptions.AutoCreateTopic || _topicName == null)
        {
            return;
        }

        try
        {
            var publisherClient = await ConnectionPool.GetPublisherAsync(PubSubEventBusOptions.ConnectionName);
            await publisherClient.GetTopicAsync(_topicName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            var publisherClient = await ConnectionPool.GetPublisherAsync(PubSubEventBusOptions.ConnectionName);
            await publisherClient.CreateTopicAsync(_topicName);
            Logger.LogInformation("Created Pub/Sub topic: {TopicName}", _topicName.ToString());
        }
    }

    private async Task EnsureSubscriptionExistsAsync()
    {
        if (!PubSubEventBusOptions.AutoCreateSubscription || _subscriptionName == null || _topicName == null)
        {
            return;
        }

        try
        {
            var subscriberClient = await ConnectionPool.GetSubscriberAsync(PubSubEventBusOptions.ConnectionName);
            await subscriberClient.GetSubscriptionAsync(_subscriptionName);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
        {
            var subscriberClient = await ConnectionPool.GetSubscriberAsync(PubSubEventBusOptions.ConnectionName);

            var request = new Subscription
            {
                SubscriptionName = _subscriptionName,
                TopicAsTopicName = _topicName,
                AckDeadlineSeconds = PubSubEventBusOptions.AckDeadlineSeconds,
                EnableMessageOrdering = PubSubEventBusOptions.EnableMessageOrdering,
                MessageRetentionDuration = Google.Protobuf.WellKnownTypes.Duration.FromTimeSpan(
                    TimeSpan.FromDays(PubSubEventBusOptions.MessageRetentionDays))
            };

            if (!string.IsNullOrEmpty(PubSubEventBusOptions.SubscriptionFilter))
            {
                request.Filter = PubSubEventBusOptions.SubscriptionFilter;
            }

            if (!string.IsNullOrEmpty(PubSubEventBusOptions.DeadLetterTopicId))
            {
                var connection = ConnectionPool.GetConnection(PubSubEventBusOptions.ConnectionName);
                var deadLetterTopicName = TopicName.FromProjectTopic(connection.ProjectId, PubSubEventBusOptions.DeadLetterTopicId);

                request.DeadLetterPolicy = new DeadLetterPolicy
                {
                    DeadLetterTopic = deadLetterTopicName.ToString(),
                    MaxDeliveryAttempts = PubSubEventBusOptions.MaxDeliveryAttempts
                };
            }

            await subscriberClient.CreateSubscriptionAsync(request);
            Logger.LogInformation("Created Pub/Sub subscription: {SubscriptionName}", _subscriptionName.ToString());
        }
    }

    private async Task StartSubscriberAsync()
    {
        if (_subscriptionName == null)
        {
            return;
        }

        var connection = ConnectionPool.GetConnection(PubSubEventBusOptions.ConnectionName);

        var builder = new SubscriberClientBuilder
        {
            SubscriptionName = _subscriptionName,
            Settings = new SubscriberClient.Settings
            {
                FlowControlSettings = new Google.Api.Gax.FlowControlSettings(
                    maxOutstandingElementCount: PubSubEventBusOptions.MaxConcurrentHandlers,
                    maxOutstandingByteCount: null)
            }
        };

        if (!string.IsNullOrEmpty(connection.EmulatorHost))
        {
            builder.Endpoint = connection.EmulatorHost;
            builder.ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure;
        }
        else if (!string.IsNullOrEmpty(connection.CredentialsPath))
        {
            builder.CredentialsPath = connection.CredentialsPath;
        }

        _subscriberClient = await builder.BuildAsync();
        _cancellationTokenSource = new CancellationTokenSource();

        // Start processing messages in the background
        _ = _subscriberClient.StartAsync(ProcessMessageAsync);
    }

    private async Task<SubscriberClient.Reply> ProcessMessageAsync(
        PubsubMessage message,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!message.Attributes.TryGetValue("EventName", out var eventName))
            {
                Logger.LogWarning("Received message without EventName attribute. MessageId: {MessageId}", message.MessageId);
                return SubscriberClient.Reply.Ack;
            }

            if (!EventTypes.TryGetValue(eventName, out var eventType))
            {
                Logger.LogDebug("No handler registered for event: {EventName}", eventName);
                return SubscriberClient.Reply.Ack;
            }

            var eventData = Serializer.Deserialize(message.Data.ToByteArray(), eventType);

            if (eventData == null)
            {
                Logger.LogWarning("Failed to deserialize event data for event: {EventName}", eventName);
                return SubscriberClient.Reply.Ack;
            }

            // Extract correlation ID from attributes
            message.Attributes.TryGetValue(EventBusConsts.CorrelationIdHeaderName, out var correlationId);

            // Process the event with inbox support
            if (await AddToInboxAsync(message.MessageId, eventName, eventType, eventData, correlationId))
            {
                return SubscriberClient.Reply.Ack;
            }

            // Trigger handlers directly if inbox is not used
            using (CorrelationIdProvider.Change(correlationId))
            {
                await TriggerHandlersDirectAsync(eventType, eventData);
            }

            return SubscriberClient.Reply.Ack;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error processing Pub/Sub message. MessageId: {MessageId}", message.MessageId);
            return SubscriberClient.Reply.Nack;
        }
    }

    public override IDisposable Subscribe(Type eventType, IEventHandlerFactory factory)
    {
        var handlerFactories = GetOrCreateHandlerFactories(eventType);

        if (factory.IsInFactories(handlerFactories))
        {
            return NullDisposable.Instance;
        }

        handlerFactories.Add(factory);

        return new EventHandlerFactoryUnregistrar(this, eventType, factory);
    }

    public override void Unsubscribe<TEvent>(Func<TEvent, Task> action)
    {
        Check.NotNull(action, nameof(action));

        GetOrCreateHandlerFactories(typeof(TEvent))
            .Locking(factories =>
            {
                factories.RemoveAll(factory =>
                {
                    if (factory is not SingleInstanceHandlerFactory singleInstanceFactory)
                    {
                        return false;
                    }

                    if (singleInstanceFactory.HandlerInstance is not ActionEventHandler<TEvent> actionHandler)
                    {
                        return false;
                    }

                    return actionHandler.Action == action;
                });
            });
    }

    public override void Unsubscribe(Type eventType, IEventHandler handler)
    {
        GetOrCreateHandlerFactories(eventType)
            .Locking(factories =>
            {
                factories.RemoveAll(factory =>
                    factory is SingleInstanceHandlerFactory singleInstance &&
                    singleInstance.HandlerInstance == handler);
            });
    }

    public override void Unsubscribe(Type eventType, IEventHandlerFactory factory)
    {
        GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Remove(factory));
    }

    public override void UnsubscribeAll(Type eventType)
    {
        GetOrCreateHandlerFactories(eventType).Locking(factories => factories.Clear());
    }

    protected override async Task PublishToEventBusAsync(Type eventType, object eventData)
    {
        await PublishAsync(eventType, eventData, correlationId: CorrelationIdProvider.Get());
    }

    protected override void AddToUnitOfWork(IUnitOfWork unitOfWork, UnitOfWorkEventRecord eventRecord)
    {
        unitOfWork.AddOrReplaceDistributedEvent(eventRecord);
    }

    public override async Task PublishFromOutboxAsync(
        OutgoingEventInfo outgoingEvent,
        OutboxConfig outboxConfig)
    {
        await PublishAsync(
            outgoingEvent.EventName,
            outgoingEvent.EventData,
            eventId: outgoingEvent.Id,
            correlationId: outgoingEvent.GetCorrelationId());

        using (CorrelationIdProvider.Change(outgoingEvent.GetCorrelationId()))
        {
            await TriggerDistributedEventSentAsync(new DistributedEventSent
            {
                Source = DistributedEventSource.Outbox,
                EventName = outgoingEvent.EventName,
                EventData = outgoingEvent.EventData
            });
        }
    }

    public override async Task PublishManyFromOutboxAsync(
        IEnumerable<OutgoingEventInfo> outgoingEvents,
        OutboxConfig outboxConfig)
    {
        foreach (var outgoingEvent in outgoingEvents)
        {
            await PublishAsync(
                outgoingEvent.EventName,
                outgoingEvent.EventData,
                eventId: outgoingEvent.Id,
                correlationId: outgoingEvent.GetCorrelationId());

            using (CorrelationIdProvider.Change(outgoingEvent.GetCorrelationId()))
            {
                await TriggerDistributedEventSentAsync(new DistributedEventSent
                {
                    Source = DistributedEventSource.Outbox,
                    EventName = outgoingEvent.EventName,
                    EventData = outgoingEvent.EventData
                });
            }
        }
    }

    public override async Task ProcessFromInboxAsync(
        IncomingEventInfo incomingEvent,
        InboxConfig inboxConfig)
    {
        var eventType = EventTypes.GetOrDefault(incomingEvent.EventName);
        if (eventType == null)
        {
            return;
        }

        var eventData = Serializer.Deserialize(incomingEvent.EventData, eventType);
        var exceptions = new List<Exception>();

        using (CorrelationIdProvider.Change(incomingEvent.GetCorrelationId()))
        {
            await TriggerHandlersFromInboxAsync(eventType, eventData!, exceptions, inboxConfig);
        }

        if (exceptions.Any())
        {
            ThrowOriginalExceptions(eventType, exceptions);
        }
    }

    protected override byte[] Serialize(object eventData)
    {
        return Serializer.Serialize(eventData);
    }

    public virtual Task PublishAsync(
        Type eventType,
        object eventData,
        Dictionary<string, string>? additionalAttributes = null,
        Guid? eventId = null,
        string? correlationId = null)
    {
        var eventName = EventNameAttribute.GetNameOrDefault(eventType);
        var body = Serializer.Serialize(eventData);

        return PublishAsync(eventName, body, additionalAttributes, eventId, correlationId);
    }

    protected virtual async Task PublishAsync(
        string eventName,
        byte[] body,
        Dictionary<string, string>? additionalAttributes = null,
        Guid? eventId = null,
        string? correlationId = null)
    {
        if (_topicName == null)
        {
            throw new AbpException("Pub/Sub Event Bus has not been initialized. Call Initialize() first.");
        }

        var connection = ConnectionPool.GetConnection(PubSubEventBusOptions.ConnectionName);

        var builder = new PublisherClientBuilder
        {
            TopicName = _topicName
        };

        if (!string.IsNullOrEmpty(connection.EmulatorHost))
        {
            builder.Endpoint = connection.EmulatorHost;
            builder.ChannelCredentials = Grpc.Core.ChannelCredentials.Insecure;
        }
        else if (!string.IsNullOrEmpty(connection.CredentialsPath))
        {
            builder.CredentialsPath = connection.CredentialsPath;
        }

        var publisherClient = await builder.BuildAsync();

        try
        {
            var message = new PubsubMessage
            {
                Data = ByteString.CopyFrom(body),
                Attributes =
                {
                    ["EventName"] = eventName,
                    ["MessageId"] = (eventId ?? GuidGenerator.Create()).ToString("N")
                }
            };

            // Add correlation ID if present
            if (!string.IsNullOrEmpty(correlationId))
            {
                message.Attributes[EventBusConsts.CorrelationIdHeaderName] = correlationId;
            }

            // Add default attributes from configuration
            foreach (var attr in PubSubEventBusOptions.DefaultMessageAttributes)
            {
                message.Attributes[attr.Key] = attr.Value;
            }

            // Add additional attributes
            if (additionalAttributes != null)
            {
                foreach (var attr in additionalAttributes)
                {
                    message.Attributes[attr.Key] = attr.Value;
                }
            }

            var messageId = await publisherClient.PublishAsync(message);

            Logger.LogDebug(
                "Published event to Pub/Sub. EventName: {EventName}, MessageId: {MessageId}",
                eventName,
                messageId);
        }
        finally
        {
            await publisherClient.ShutdownAsync(TimeSpan.FromSeconds(10));
        }
    }

    protected override Task OnAddToOutboxAsync(string eventName, Type eventType, object eventData)
    {
        EventTypes.GetOrAdd(eventName, eventType);
        return base.OnAddToOutboxAsync(eventName, eventType, eventData);
    }

    private List<IEventHandlerFactory> GetOrCreateHandlerFactories(Type eventType)
    {
        return HandlerFactories.GetOrAdd(
            eventType,
            type =>
            {
                var eventName = EventNameAttribute.GetNameOrDefault(type);
                EventTypes.GetOrAdd(eventName, eventType);
                return new List<IEventHandlerFactory>();
            });
    }

    protected override IEnumerable<EventTypeWithEventHandlerFactories> GetHandlerFactories(Type eventType)
    {
        var handlerFactoryList = new List<EventTypeWithEventHandlerFactories>();

        foreach (var handlerFactory in HandlerFactories.Where(hf => ShouldTriggerEventForHandler(eventType, hf.Key)))
        {
            handlerFactoryList.Add(new EventTypeWithEventHandlerFactories(handlerFactory.Key, handlerFactory.Value));
        }

        return handlerFactoryList.ToArray();
    }

    private static bool ShouldTriggerEventForHandler(Type targetEventType, Type handlerEventType)
    {
        // Should trigger same type
        if (handlerEventType == targetEventType)
        {
            return true;
        }

        // Should trigger for inherited types
        if (handlerEventType.IsAssignableFrom(targetEventType))
        {
            return true;
        }

        return false;
    }

    public async Task StopAsync()
    {
        if (_subscriberClient != null)
        {
            await _subscriberClient.StopAsync(CancellationToken.None);
        }

        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
    }
}
