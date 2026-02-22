using Bdaya.Abp.EventBus.PubSub;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Volo.Abp;
using Volo.Abp.EventBus.Distributed;

namespace Bdaya.Abp.EventBus.PubSub.Tests;

/// <summary>
/// Integration tests for the Pub/Sub distributed event bus.
/// Uses a real Pub/Sub emulator running in Docker.
/// </summary>
public class PubSubDistributedEventBusTests : IClassFixture<PubSubEmulatorFixture>, IAsyncLifetime
{
    private readonly PubSubEmulatorFixture _fixture;
    private IAbpApplicationWithInternalServiceProvider? _application;

    public PubSubDistributedEventBusTests(PubSubEmulatorFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Configure the module to use the emulator BEFORE creating the ABP application
        PubSubTestModule.EmulatorHost = _fixture.EmulatorHost;
        PubSubTestModule.ProjectId = _fixture.ProjectId;

        // Create and initialize ABP application
        _application = await AbpApplicationFactory.CreateAsync<PubSubTestModule>();
        await _application.InitializeAsync();

        TestOrderCreatedHandler.Reset();
        TestOrderCompletedHandler.Reset();

        // Give the event bus time to initialize
        await Task.Delay(500, TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_application != null)
        {
            await _application.ShutdownAsync();
        }
    }

    protected T GetRequiredService<T>() where T : notnull
    {
        return _application!.ServiceProvider.GetRequiredService<T>();
    }

    [Fact]
    public async Task Should_Publish_And_Receive_Event()
    {
        // Arrange
        var eventBus = GetRequiredService<IDistributedEventBus>();
        var orderId = Guid.NewGuid();
        var eventData = new OrderCreatedEto
        {
            OrderId = orderId,
            CustomerName = "Test Customer",
            Amount = 99.99m,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        await eventBus.PublishAsync(eventData);

        // Assert - Wait for the event to be received (with timeout)
        var receivedEvent = await WaitForEventAsync(
            TestOrderCreatedHandler.WaitHandle!,
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);

        receivedEvent.ShouldNotBeNull();
        receivedEvent.OrderId.ShouldBe(orderId);
        receivedEvent.CustomerName.ShouldBe("Test Customer");
        receivedEvent.Amount.ShouldBe(99.99m);
    }

    [Fact]
    public async Task Should_Handle_Multiple_Events()
    {
        // Arrange
        var eventBus = GetRequiredService<IDistributedEventBus>();
        var orderIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        // Act
        foreach (var orderId in orderIds)
        {
            await eventBus.PublishAsync(new OrderCreatedEto
            {
                OrderId = orderId,
                CustomerName = $"Customer-{orderId}",
                Amount = 50.00m,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Assert - Wait for all events
        await Task.Delay(3000, TestContext.Current.CancellationToken); // Give time for all events to be processed

        TestOrderCreatedHandler.ReceivedEvents.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Should_Handle_Different_Event_Types()
    {
        // Arrange
        var eventBus = GetRequiredService<IDistributedEventBus>();
        var orderId = Guid.NewGuid();

        // Act
        await eventBus.PublishAsync(new OrderCreatedEto
        {
            OrderId = orderId,
            CustomerName = "Multi-Type Test",
            Amount = 100.00m,
            CreatedAt = DateTime.UtcNow
        });

        await eventBus.PublishAsync(new OrderCompletedEto
        {
            OrderId = orderId,
            CompletedAt = DateTime.UtcNow
        });

        // Assert
        await Task.Delay(2000, TestContext.Current.CancellationToken);

        TestOrderCreatedHandler.ReceivedEvents.ShouldContain(e => e.OrderId == orderId);
        TestOrderCompletedHandler.ReceivedEvents.ShouldContain(e => e.OrderId == orderId);
    }

    [Fact]
    public void Should_Resolve_EventBus_As_PubSub_Implementation()
    {
        // Arrange & Act
        var eventBus = GetRequiredService<IDistributedEventBus>();

        // Assert
        eventBus.ShouldBeOfType<PubSubDistributedEventBus>();
    }

    [Fact]
    public void Should_Resolve_IPubSubDistributedEventBus()
    {
        // Arrange & Act
        var eventBus = GetRequiredService<IPubSubDistributedEventBus>();

        // Assert
        eventBus.ShouldNotBeNull();
        eventBus.ShouldBeOfType<PubSubDistributedEventBus>();
    }

    private static async Task<T?> WaitForEventAsync<T>(TaskCompletionSource<T> tcs, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var delayTask = Task.Delay(timeout, cancellationToken);
        var completedTask = await Task.WhenAny(tcs.Task, delayTask);

        if (completedTask == delayTask)
        {
            return default;
        }

        return await tcs.Task;
    }
}
