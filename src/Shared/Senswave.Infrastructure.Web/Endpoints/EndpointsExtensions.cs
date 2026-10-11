using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;

namespace Senswave.Infrastructure.Web.Endpoints;

public static class EndpointsExtensions
{
    public static IServiceCollection AddMinimalApiEndpoints(this IServiceCollection services, Assembly assembly)
    {
        var serviceDescriptors = assembly
            .DefinedTypes
            .Where(type => type is { IsAbstract: false, IsInterface: false } &&
                           type.IsAssignableTo(typeof(IEndpoint)))
            .Select(type => ServiceDescriptor.Transient(typeof(IEndpoint), type))
            .ToArray();

        services.TryAddEnumerable(serviceDescriptors);

        return services;
    }

    public static void MapEndpoints(this IEndpointRouteBuilder endpointRouteBuilder, IEnumerable<IEndpoint> endpoints)
    {
        foreach (IEndpoint endpoint in endpoints)
        {
            if (endpoint is IPublicEndpoint)
                continue;

            endpoint.Map(endpointRouteBuilder);
        }
    }

    /// <summary>
    /// Maps only <see cref="INavigationEndpoint"/>s. Used in gRPC transport modes, where no other endpoint is mapped.
    /// </summary>
    public static void MapNavigationEndpoints(this IEndpointRouteBuilder endpointRouteBuilder, IEnumerable<IEndpoint> endpoints)
    {
        foreach (IEndpoint endpoint in endpoints)
        {
            if (endpoint is not INavigationEndpoint)
                continue;

            endpoint.Map(endpointRouteBuilder);
        }
    }

    public static void MapPublicEndpoints(this IEndpointRouteBuilder endpointRouteBuilder, IEnumerable<IEndpoint> endpoints)
    {
        foreach (IEndpoint endpoint in endpoints)
        {
            if (endpoint is not IPublicEndpoint)
                continue;

            endpoint.Map(endpointRouteBuilder);
        }
    }
}
