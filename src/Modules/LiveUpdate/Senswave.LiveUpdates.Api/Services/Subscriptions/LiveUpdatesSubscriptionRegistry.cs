using Microsoft.Extensions.Options;
using Senswave.Contracts.LiveUpdates.V1;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Senswave.LiveUpdates.Api.Services.Subscriptions;

internal sealed class LiveUpdatesSubscriptionRegistry(IOptions<LiveUpdatesOptions> options) : ILiveUpdatesSubscriptionRegistry
{
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, Subscription>> _groups = new();

    public ILiveUpdatesSubscription Subscribe(IReadOnlyCollection<string> groupNames)
    {
        // Bounded with drop-oldest: a stalled client loses old updates instead of growing memory without limit.
        var channel = Channel.CreateBounded<LiveUpdate>(new BoundedChannelOptions(options.Value.SubscriptionBufferSize)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        var subscription = new Subscription(this, channel, groupNames);

        foreach (var groupName in groupNames)
            _groups.GetOrAdd(groupName, _ => new()).TryAdd(subscription.Id, subscription);

        return subscription;
    }

    public Task PublishAsync(string groupName, LiveUpdate update, CancellationToken cancellationToken)
    {
        if (!_groups.TryGetValue(groupName, out var subscriptions))
            return Task.CompletedTask;

        // TryWrite never blocks on a drop-oldest channel; it only fails for a stream that is closing, which is fine to skip.
        foreach (var subscription in subscriptions.Values)
            subscription.Writer.TryWrite(update);

        return Task.CompletedTask;
    }

    private void Remove(Subscription subscription)
    {
        foreach (var groupName in subscription.GroupNames)
        {
            if (!_groups.TryGetValue(groupName, out var subscriptions))
                continue;

            subscriptions.TryRemove(subscription.Id, out _);

            if (subscriptions.IsEmpty)
                _groups.TryRemove(new KeyValuePair<string, ConcurrentDictionary<Guid, Subscription>>(groupName, subscriptions));
        }
    }

    private sealed class Subscription(
        LiveUpdatesSubscriptionRegistry registry,
        Channel<LiveUpdate> channel,
        IReadOnlyCollection<string> groupNames) : ILiveUpdatesSubscription
    {
        private int _disposed;

        public Guid Id { get; } = Guid.NewGuid();

        public IReadOnlyCollection<string> GroupNames { get; } = groupNames;

        public ChannelReader<LiveUpdate> Reader => channel.Reader;

        public ChannelWriter<LiveUpdate> Writer => channel.Writer;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            registry.Remove(this);
            channel.Writer.TryComplete();
        }
    }
}
