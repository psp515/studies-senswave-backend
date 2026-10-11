using Senswave.Contracts.LiveUpdates.V1;
using Senswave.LiveUpdates.Api.Diagnostics;
using Senswave.LiveUpdates.Api.Extensions;
using Senswave.LiveUpdates.Api.Services.Subscriptions;

namespace Senswave.LiveUpdates.Api.Services.DataSourcesUpdates;

/// <summary>
/// gRPC counterpart of <see cref="DataSourcesUpdateService"/>: typed <see cref="LiveUpdate"/> messages through the registry.
/// </summary>
internal sealed class GrpcDataSourcesUpdateService(
    ILiveUpdatesActivityProvider activityProvider,
    ILiveUpdatesSubscriptionRegistry registry,
    ILogger<GrpcDataSourcesUpdateService> logger) : IDataSourcesUpdateService
{
    public async Task UpdateDataSourceState(Guid dataSourceId, string state, CancellationToken cancellationToken)
    {
        using var activity = activityProvider.StartActivity("LiveUpdates /update");

        var groupName = dataSourceId.ToDataSourcesGroupName();
        activity?.AddTag("rpc.live_updates.group", groupName);
        activity?.AddTag("rpc.live_updates.type", LiveUpdate.UpdateOneofCase.DataSourceState.ToString());
        activity?.AddTag("rpc.live_updates.datasource_id", dataSourceId);

        logger.LogInformation("[DataSource: {dataSourceId}] Data source state update: {state}", dataSourceId, state);

        await registry.PublishAsync(groupName, new LiveUpdate
        {
            DataSourceState = new DataSourceStateUpdate { DataSourceId = dataSourceId.ToString(), State = state }
        }, cancellationToken);
    }
}
