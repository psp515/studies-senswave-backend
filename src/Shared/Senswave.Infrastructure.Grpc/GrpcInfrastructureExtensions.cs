using Grpc.AspNetCore.Server;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Infrastructure.Grpc.Interceptors;

namespace Senswave.Infrastructure.Grpc;

public static class GrpcInfrastructureExtensions
{
    /// <summary>
    /// Registers gRPC hosting with <see cref="ErrorHandlingInterceptor"/> first in the interceptor list.
    /// <paramref name="configure"/> may append further interceptors (e.g. the Users consent interceptor).
    /// </summary>
    public static IServiceCollection AddGrpcInfrastructure(this IServiceCollection services, Action<GrpcServiceOptions>? configure = null)
    {
        services.AddGrpc(options =>
        {
            options.Interceptors.Add<ErrorHandlingInterceptor>();
            configure?.Invoke(options);
        });

        return services;
    }
}
