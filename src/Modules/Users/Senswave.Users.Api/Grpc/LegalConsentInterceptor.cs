using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.Extensions.Logging;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Users.Domain.Interfaces;
using System.Diagnostics;

namespace Senswave.Users.Api.Grpc;

/// <summary>
/// gRPC counterpart of <c>LegalMiddleware</c>, and the only consent check in gRPC transport modes: blocks authenticated
/// users without the latest consents. Users (incl. auth and legal) and live updates are exempt, as they are in REST.
/// </summary>
public sealed class LegalConsentInterceptor(
    IRequestContext requestContext,
    IUserService userService,
    ILogger<LegalConsentInterceptor> logger) : Interceptor
{
    private static readonly string[] ExemptPackages =
    [
        "/senswave.users.v1.",
        "/senswave.liveupdates.v1.",
    ];

    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        await EnsureConsents(context);

        return await continuation(request, context);
    }

    public override async Task ServerStreamingServerHandler<TRequest, TResponse>(
        TRequest request,
        IServerStreamWriter<TResponse> responseStream,
        ServerCallContext context,
        ServerStreamingServerMethod<TRequest, TResponse> continuation)
    {
        await EnsureConsents(context);

        await continuation(request, responseStream, context);
    }

    private async Task EnsureConsents(ServerCallContext context)
    {
        if (requestContext.UserId == Guid.Empty || IsExempt(context.Method))
            return;

        var hasConsents = await userService.UserHasLatestConsents(requestContext.UserId, context.CancellationToken);

        if (hasConsents.IsSuccess)
            return;

        logger.LogWarning("[UserId: {UserId}] User does not have latest consents. Blocking request.",
            requestContext.UserId);

        throw new RpcException(
            new Status(StatusCode.PermissionDenied, "User does not have latest consents."),
            GrpcErrorMetadata.Create(hasConsents.Errors, Activity.Current?.Id));
    }

    private static bool IsExempt(string method) =>
        ExemptPackages.Any(package => method.StartsWith(package, StringComparison.Ordinal));
}
