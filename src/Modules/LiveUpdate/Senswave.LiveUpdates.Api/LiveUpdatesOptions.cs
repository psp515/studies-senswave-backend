using Senswave.LiveUpdates.Api.Options;

namespace Senswave.LiveUpdates.Api;

public class LiveUpdatesOptions
{
    public const string SectionName = "Modules:LiveUpdates";

    public RateLimiterOptions RateLimiter { get; set; } = new();

    /// <summary>
    /// Per-stream buffer of the gRPC live-updates path. When a client falls behind, the oldest updates are dropped.
    /// </summary>
    public int SubscriptionBufferSize { get; set; } = 64;
}
