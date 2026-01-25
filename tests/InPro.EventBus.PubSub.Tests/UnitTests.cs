using Bdaya.Abp.EventBus.PubSub;
using Shouldly;

namespace Bdaya.Abp.EventBus.PubSub.Tests;

/// <summary>
/// Unit tests for the serializer.
/// </summary>
public class PubSubSerializerTests
{
    private readonly PubSubSerializer _serializer = new();

    [Fact]
    public void Should_Serialize_And_Deserialize_Event()
    {
        // Arrange
        var original = new OrderCreatedEto
        {
            OrderId = Guid.NewGuid(),
            CustomerName = "Test Customer",
            Amount = 123.45m,
            CreatedAt = DateTime.UtcNow
        };

        // Act
        var bytes = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<OrderCreatedEto>(bytes);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.OrderId.ShouldBe(original.OrderId);
        deserialized.CustomerName.ShouldBe(original.CustomerName);
        deserialized.Amount.ShouldBe(original.Amount);
    }

    [Fact]
    public void Should_Deserialize_With_Type()
    {
        // Arrange
        var original = new OrderCompletedEto
        {
            OrderId = Guid.NewGuid(),
            CompletedAt = DateTime.UtcNow
        };

        // Act
        var bytes = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize(bytes, typeof(OrderCompletedEto)) as OrderCompletedEto;

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.OrderId.ShouldBe(original.OrderId);
    }

    [Fact]
    public void Should_Handle_Empty_Values()
    {
        // Arrange
        var original = new OrderCreatedEto
        {
            OrderId = Guid.Empty,
            CustomerName = string.Empty,
            Amount = 0
        };

        // Act
        var bytes = _serializer.Serialize(original);
        var deserialized = _serializer.Deserialize<OrderCreatedEto>(bytes);

        // Assert
        deserialized.ShouldNotBeNull();
        deserialized.OrderId.ShouldBe(Guid.Empty);
        deserialized.CustomerName.ShouldBe(string.Empty);
        deserialized.Amount.ShouldBe(0);
    }
}

/// <summary>
/// Unit tests for options configuration.
/// </summary>
public class PubSubOptionsTests
{
    [Fact]
    public void AbpPubSubOptions_Should_Have_Default_Connection()
    {
        // Arrange & Act
        var options = new AbpPubSubOptions();

        // Assert
        options.Connections.ShouldContainKey("Default");
        options.Default.ShouldNotBeNull();
    }

    [Fact]
    public void AbpPubSubOptions_Default_Should_Access_Default_Connection()
    {
        // Arrange
        var options = new AbpPubSubOptions();

        // Act
        options.Default.ProjectId = "my-project";

        // Assert
        options.Connections["Default"].ProjectId.ShouldBe("my-project");
    }

    [Fact]
    public void AbpPubSubEventBusOptions_Should_Have_Sensible_Defaults()
    {
        // Arrange & Act
        var options = new AbpPubSubEventBusOptions();

        // Assert
        options.MaxMessages.ShouldBe(10);
        options.AckDeadlineSeconds.ShouldBe(60);
        options.AutoCreateTopic.ShouldBeTrue();
        options.AutoCreateSubscription.ShouldBeTrue();
        options.MaxConcurrentHandlers.ShouldBe(1);
        options.EnableMessageOrdering.ShouldBeFalse();
        options.MaxDeliveryAttempts.ShouldBe(5);
        options.MessageRetentionDays.ShouldBe(7);
    }

    [Fact]
    public void PubSubConnectionConfiguration_Should_Support_Emulator()
    {
        // Arrange
        var config = new PubSubConnectionConfiguration
        {
            ProjectId = "test-project",
            EmulatorHost = "localhost:8085"
        };

        // Assert
        config.ProjectId.ShouldBe("test-project");
        config.EmulatorHost.ShouldBe("localhost:8085");
        config.CredentialsPath.ShouldBeNull();
    }
}
