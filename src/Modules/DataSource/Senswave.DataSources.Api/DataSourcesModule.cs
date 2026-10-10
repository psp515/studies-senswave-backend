using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;
using Senswave.Abstractions.Modules;
using Senswave.DataSources.Api.Grpc;
using Senswave.DataSources.Application;
using Senswave.DataSources.BrokerConnection;
using Senswave.DataSources.BrokerConnection.Features.Status;
using Senswave.DataSources.Domain;
using Senswave.DataSources.Domain.Brokers.Brokers.Options;
using Senswave.DataSources.Domain.Diagnostics;
using Senswave.DataSources.Infrastructure;
using Senswave.Infrastructure.Grpc.Modules;

namespace Senswave.DataSources.Api;

public class DataSourcesModule : ISenswaveModule, ISenswaveGrpcModule
{
    public const string DefaultListenerName = DataSourcesActivityProvider.DefaultListenerName;

    public const string BrokersTag = $"{ModuleName} - Brokers";
    public const string BrokerClientTag = $"{ModuleName} - Broker Clients";
    public const string BrokerSessionsTag = $"{ModuleName} - Broker Sessions";
    public const string BrokerSubscribtionsTag = $"{ModuleName} - Broker Subscriptions";

    public static string GroupName => ModuleName.ToLower();

    public const string ModuleName = "Data Sources";

    public string Name => ModuleName;

    public void Register(IServiceCollection services, IConfiguration configuration)
    {
        services.AddDataSourcesDomain(configuration)
            .AddDataSourcesInfrastructure()
            .AddDataSourcesApplication(configuration);

        var assembly = typeof(DataSourcesModule).Assembly;

        services.AddMinimalApiEndpoints(assembly);

        var options = configuration.GetSection(BrokerOptions.SectionName).Get<BrokerOptions>();

        if (options!.IsCluster)
        {
            services.AddHealthChecks()
                .AddCheck<WorkerStatusHealthCheck>("datasourceworker", tags: new[] { ModuleName, BrokersTag });

            return;
        }

        services.AddBrokerClientServices(configuration);
    }

    public void MapGrpcServices(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGrpcService<DataSourcesGrpcService>();
    }
}