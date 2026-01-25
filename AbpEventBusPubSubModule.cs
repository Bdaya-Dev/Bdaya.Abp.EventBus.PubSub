using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp;
using Volo.Abp.EventBus;
using Volo.Abp.Modularity;

namespace Bdaya.Abp.EventBus.PubSub;

/// <summary>
/// ABP module for Google Cloud Pub/Sub distributed event bus integration.
/// Add this module as a dependency to use Pub/Sub for distributed events.
/// </summary>
[DependsOn(typeof(AbpEventBusModule))]
public class AbpEventBusPubSubModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();

        // Configure Pub/Sub connection options from configuration
        Configure<AbpPubSubOptions>(configuration.GetSection("PubSub"));

        // Configure event bus options from configuration
        Configure<AbpPubSubEventBusOptions>(configuration.GetSection("PubSub:EventBus"));
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context
            .ServiceProvider
            .GetRequiredService<IPubSubDistributedEventBus>()
            .InitializeAsync();
    }

    public override async Task OnApplicationShutdownAsync(ApplicationShutdownContext context)
    {
        var eventBus = context.ServiceProvider.GetRequiredService<IPubSubDistributedEventBus>();

        if (eventBus is PubSubDistributedEventBus pubSubEventBus)
        {
            await pubSubEventBus.StopAsync();
        }
    }
}
