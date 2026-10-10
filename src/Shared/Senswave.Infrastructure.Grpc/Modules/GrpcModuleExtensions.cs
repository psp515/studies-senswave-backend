using Microsoft.AspNetCore.Routing;
using Senswave.Abstractions.Modules;

namespace Senswave.Infrastructure.Grpc.Modules;

public static class GrpcModuleExtensions
{
    public static IEndpointRouteBuilder MapGrpcModules(this IEndpointRouteBuilder endpoints, IEnumerable<ISenswaveModule> modules)
    {
        foreach (var module in modules.OfType<ISenswaveGrpcModule>())
            module.MapGrpcServices(endpoints);

        return endpoints;
    }
}
