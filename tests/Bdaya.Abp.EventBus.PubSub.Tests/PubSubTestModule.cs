using Bdaya.Abp.EventBus.PubSub;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.Autofac;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Modularity;

namespace Bdaya.Abp.EventBus.PubSub.Tests;

/// <summary>
/// Test module that configures the Pub/Sub event bus for testing.
/// </summary>
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(AbpEventBusPubSubModule)
)]
public class PubSubTestModule : AbpModule
{
    public static string? EmulatorHost { get; set; }
    public static string? ProjectId { get; set; }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpPubSubOptions>(options =>
        {
            options.Default.ProjectId = ProjectId ?? "test-project";
            options.Default.EmulatorHost = EmulatorHost ?? "localhost:8085";
        });

        Configure<AbpPubSubEventBusOptions>(options =>
        {
            options.TopicId = "test-events";
            options.SubscriptionId = "test-subscription";
            options.AutoCreateTopic = true;
            options.AutoCreateSubscription = true;
            options.MaxConcurrentHandlers = 1;
        });

        // Explicitly configure distributed event bus handlers for the test events
        Configure<AbpDistributedEventBusOptions>(options =>
        {
            options.Handlers.Add<TestOrderCreatedHandler>();
            options.Handlers.Add<TestOrderCompletedHandler>();
        });

        // Register handlers as transient services
        context.Services.AddTransient<TestOrderCreatedHandler>();
        context.Services.AddTransient<TestOrderCompletedHandler>();
    }
}
