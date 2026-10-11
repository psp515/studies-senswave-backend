using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Senswave.Abstractions.Contexts;
using Senswave.Contracts.LiveUpdates.V1;
using Senswave.Integration.Devices.Devices;
using Senswave.Integration.Homes.Home;
using Senswave.LiveUpdates.Api.Extensions;
using Senswave.LiveUpdates.Api.Services.Subscriptions;

namespace Senswave.LiveUpdates.Api.Grpc;

/// <summary>
/// gRPC counterpart of <c>LiveUpdatesHub</c>: one <c>Subscribe</c> call replaces connect + <c>Initialize</c>.
/// Unlike the hub, a subscription that cannot be established fails the call instead of returning silently.
/// The live-updates rate limiter is not applied (rate limiting is out of scope for gRPC).
/// </summary>
[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class LiveUpdatesGrpcService(
    IRequestClient<HomeRequest> homeRequestClient,
    IRequestClient<DevicesRequest> devicesRequestClient,
    ILiveUpdatesSubscriptionRegistry registry,
    IRequestContext requestContext,
    ILogger<LiveUpdatesGrpcService> logger) : LiveUpdatesService.LiveUpdatesServiceBase
{
    public override async Task Subscribe(SubscribeRequest request, IServerStreamWriter<LiveUpdate> responseStream, ServerCallContext context)
    {
        var userId = requestContext.UserId;
        var cancellationToken = context.CancellationToken;

        if (!Guid.TryParse(request.HomeReferenceId, out var homeId))
        {
            logger.LogWarning("[User: {userId}] Argument is not valid home guid: {homeReferenceId}",
                userId,
                request.HomeReferenceId);

            throw new RpcException(new Status(StatusCode.InvalidArgument, "Argument is not valid home guid."));
        }

        var homeResponse = await homeRequestClient.GetResponse<HomeResponse>(new HomeRequest { HomeId = homeId }, cancellationToken);

        if (homeResponse.Message.IsFailure)
        {
            logger.LogWarning("[User: {userId}][Home: {homeId}] Home not found.", userId, homeId);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "No access to home."));
        }

        if (homeResponse.Message.OwnerId != userId && !homeResponse.Message.AllowedUsers.Contains(userId))
        {
            logger.LogWarning("[User: {userId}][Home: {homeId}] User is not allowed to access home.", userId, homeId);
            throw new RpcException(new Status(StatusCode.PermissionDenied, "No access to home."));
        }

        var groupNames = new List<string>();

        var devicesResponse = await devicesRequestClient.GetResponse<DevicesResponse>(
            new DevicesRequest { UserId = userId, HomeId = homeId }, cancellationToken);

        if (devicesResponse.Message.IsSuccess)
        {
            logger.LogInformation("[User: {userId}][Home: {homeId}] Adding user to devices groups.", userId, homeId);
            groupNames.AddRange(devicesResponse.Message.Devices.Select(x => x.ToDevicesGroupName()));
        }

        if (homeResponse.Message.DataSourceId.HasValue)
        {
            logger.LogInformation("[User: {userId}][Home: {homeId}] Adding user to data sources groups.", userId, homeId);
            groupNames.Add(homeResponse.Message.DataSourceId.Value.ToDataSourcesGroupName());
        }

        using var subscription = registry.Subscribe(groupNames);

        await responseStream.WriteAsync(new LiveUpdate { Initialized = new Initialized() }, cancellationToken);

        logger.LogInformation("[User: {userId}][Home: {homeId}][Subscription: {subscriptionId}] Initialized live update.",
            userId, homeId, subscription.Id);

        try
        {
            await foreach (var update in subscription.Reader.ReadAllAsync(cancellationToken))
                await responseStream.WriteAsync(update, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client closed the stream (switching home, leaving the app).
        }

        logger.LogInformation("[User: {userId}][Subscription: {subscriptionId}] Live update stream closed.", userId, subscription.Id);
    }
}
