using Senswave.Contracts.LiveUpdates.V1;

namespace Senswave.LiveUpdates.Api.Services.Subscriptions;

/// <summary>
/// In-process fan-out of live updates to gRPC streams, keyed by the same group names the SignalR hub uses.
/// </summary>
public interface ILiveUpdatesSubscriptionRegistry
{
    ILiveUpdatesSubscription Subscribe(IReadOnlyCollection<string> groupNames);

    Task PublishAsync(string groupName, LiveUpdate update, CancellationToken cancellationToken);
}
