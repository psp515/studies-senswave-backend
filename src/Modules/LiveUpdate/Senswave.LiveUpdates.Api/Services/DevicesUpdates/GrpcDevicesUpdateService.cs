using Senswave.Contracts.LiveUpdates.V1;
using Senswave.LiveUpdates.Api.Diagnostics;
using Senswave.LiveUpdates.Api.Extensions;
using Senswave.LiveUpdates.Api.Services.Subscriptions;

namespace Senswave.LiveUpdates.Api.Services.DevicesUpdates;

/// <summary>
/// gRPC counterpart of <see cref="DevicesUpdateService"/>: typed <see cref="LiveUpdate"/> messages through the registry.
/// </summary>
internal sealed class GrpcDevicesUpdateService(
    ILiveUpdatesActivityProvider activityProvider,
    ILiveUpdatesSubscriptionRegistry registry,
    ILogger<GrpcDevicesUpdateService> logger) : IDevicesUpdateService
{
    public Task UpdateDeviceTile(Guid deviceId) =>
        Publish(deviceId, LiveUpdate.UpdateOneofCase.DeviceTile, new LiveUpdate
        {
            DeviceTile = new DeviceTileUpdate { DeviceId = deviceId.ToString() }
        });

    public Task UpdateWidgets(Guid deviceId, List<Guid> widgets)
    {
        if (widgets.Count == 0)
        {
            using var activity = activityProvider.StartActivity("LiveUpdates /update");
            logger.LogWarning("[Device: {deviceId}] No widgets to update.", deviceId);
            activity?.AddEvent(new("No widgets to update"));
            return Task.CompletedTask;
        }

        logger.LogInformation("[Device: {deviceId}] Device update for widgets: {widgetCount} widgets.", deviceId, widgets.Count);

        return Publish(deviceId, LiveUpdate.UpdateOneofCase.Widgets, new LiveUpdate
        {
            Widgets = new WidgetsUpdate
            {
                DeviceId = deviceId.ToString(),
                WidgetIds = { widgets.Select(x => x.ToString()) }
            }
        });
    }

    public Task UpdateDevicePresence(Guid deviceId) =>
        Publish(deviceId, LiveUpdate.UpdateOneofCase.DevicePresence, new LiveUpdate
        {
            DevicePresence = new DevicePresenceUpdate { DeviceId = deviceId.ToString() }
        });

    private async Task Publish(Guid deviceId, LiveUpdate.UpdateOneofCase type, LiveUpdate update)
    {
        using var activity = activityProvider.StartActivity("LiveUpdates /update");

        var groupName = deviceId.ToDevicesGroupName();
        activity?.AddTag("rpc.live_updates.group", groupName);
        activity?.AddTag("rpc.live_updates.type", type.ToString());
        activity?.AddTag("rpc.live_updates.device_id", deviceId);

        await registry.PublishAsync(groupName, update, CancellationToken.None);
    }
}
