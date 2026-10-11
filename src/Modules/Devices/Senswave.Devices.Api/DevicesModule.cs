using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Abstractions.Modules;
using Senswave.Devices.Api.Grpc;
using Senswave.Devices.Application;
using Senswave.Devices.Domain;
using Senswave.Devices.Infrastructure;
using Senswave.Infrastructure.Grpc.Modules;

namespace Senswave.Devices.Api;

public class DevicesModule : ISenswaveModule, ISenswaveGrpcModule
{
    public const string ModuleName = "Devices";

    public const string DevicesTag = $"{ModuleName} - Devices";
    public const string DashboardsTag = $"{ModuleName} - Dashboards";
    public const string WidgetsTag = $"{ModuleName} - Widgets";
    public const string OperationsTag = $"{ModuleName} - Operations";
    public const string SharingTag = $"{ModuleName} - Sharing";

    public static string GroupName => ModuleName.ToLower();

    public string Name => ModuleName;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DevicesModule).Assembly;
        services.AddMinimalApiEndpoints(assembly);

        services.AddDevicesInfrastructure();
        services.AddDevicesApplication();
        services.AddDevicesDomain(configuration);
    }

    public void MapGrpcServices(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<DevicesGrpcService>();
        endpoints.MapGrpcService<DashboardsGrpcService>();
        endpoints.MapGrpcService<OperationsGrpcService>();
        endpoints.MapGrpcService<SharingGrpcService>();
        endpoints.MapGrpcService<WidgetsGrpcService>();
    }
}