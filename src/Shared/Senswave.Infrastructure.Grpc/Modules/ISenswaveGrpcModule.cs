using Microsoft.AspNetCore.Routing;

namespace Senswave.Infrastructure.Grpc.Modules;

/// <summary>
/// Implemented by an <c>ISenswaveModule</c> that exposes gRPC services. Kept out of <c>Senswave.Abstractions</c>
/// so that project stays free of ASP.NET dependencies.
/// </summary>
public interface ISenswaveGrpcModule
{
    /// <summary>
    /// Maps the module's gRPC services with explicit <c>endpoints.MapGrpcService&lt;T&gt;()</c> calls.
    /// Called by the host only in gRPC transport modes.
    /// </summary>
    void MapGrpcServices(IEndpointRouteBuilder endpoints);
}
