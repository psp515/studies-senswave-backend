using Senswave.Contracts.LiveUpdates.V1;
using System.Threading.Channels;

namespace Senswave.LiveUpdates.Api.Services.Subscriptions;

/// <summary>
/// One gRPC <c>Subscribe</c> stream: the gRPC counterpart of a SignalR connection. Disposing it leaves every group.
/// </summary>
public interface ILiveUpdatesSubscription : IDisposable
{
    Guid Id { get; }

    ChannelReader<LiveUpdate> Reader { get; }
}
