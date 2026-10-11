using MassTransit.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Senswave.Abstractions.Modules;
using Senswave.Abstractions.Transport;
using Senswave.Infrastructure.Grpc.Modules;
using Senswave.LiveUpdates.Api.Consumers;
using Senswave.LiveUpdates.Api.Diagnostics;
using Senswave.LiveUpdates.Api.Grpc;
using Senswave.LiveUpdates.Api.Services.DataSourcesUpdates;
using Senswave.LiveUpdates.Api.Services.DevicesUpdates;
using Senswave.LiveUpdates.Api.Services.RateLimiter;
using Senswave.LiveUpdates.Api.Services.Subscriptions;

namespace Senswave.LiveUpdates.Api;

public class LiveUpdatesModule : ISenswaveModule, ISenswaveGrpcModule
{
    public const string DefaultListenerName = "Senswave.LiveUpdate";

    public const string InitializationTag = $"{ModuleName} - Initialization";
    public const string LiveUpdatesTag = $"{ModuleName} - Live Updates";

    public const string GroupName = "live update";
    public const string ModuleName = "Live Update";

    public string Name => ModuleName;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterConsumer<DeviceTileActionEventConsumer>();
        services.RegisterConsumer<WidgetActionEventConsumer>();
        services.RegisterConsumer<DataSourceStateConsumer>();
        services.RegisterConsumer<DevicePresenceEventConsumer>();

        // Exactly one live-updates transport is registered: the SignalR hub (Rest) or the gRPC stream (Grpc/GrpcWeb).
        if (TransportOptions.FromConfiguration(configuration).IsGrpc)
        {
            services.AddSingleton<ILiveUpdatesSubscriptionRegistry, LiveUpdatesSubscriptionRegistry>();
            services.AddScoped<IDevicesUpdateService, GrpcDevicesUpdateService>();
            services.AddScoped<IDataSourcesUpdateService, GrpcDataSourcesUpdateService>();
        }
        else
        {
            services.AddSignalR();
            services.AddScoped<IDevicesUpdateService, DevicesUpdateService>();
            services.AddScoped<IDataSourcesUpdateService, DataSourcesUpdateService>();
            services.AddSingleton<IRateLimiterService, RateLimitingService>();
        }

        services.AddSingleton<ILiveUpdatesActivityProvider, LiveUpdatesActivityProvider>();

        services.Configure<LiveUpdatesOptions>(configuration.GetSection(LiveUpdatesOptions.SectionName));
    }

    public void MapGrpcServices(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<LiveUpdatesGrpcService>();
    }
}