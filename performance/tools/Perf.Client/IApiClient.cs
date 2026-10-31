using Perf.Core.Runs;

namespace Perf.Client;

public interface ILiveUpdateSubscription : IAsyncDisposable { }

public interface IApiClient : IAsyncDisposable
{
    Task<string> Login(string email, string password, CancellationToken ct);

    /// <summary>Starts the current user's MQTT broker client inside the API. Required after every DB reset or API start,
    /// otherwise tile actions fail with 400 "Broker client does not exist". An already started client counts as success.</summary>
    Task StartBrokerClient(string accessToken, string username, string password, CancellationToken ct);

    /// <summary>Current home, the seeded Pico controller device and its first dashboard.</summary>
    Task<UserData> Discover(string email, string accessToken, CancellationToken ct);

    Task TileAction(string accessToken, string deviceId, bool value, CancellationToken ct);

    /// <summary>Completes once the server confirmed the subscription (SignalR Initialized / gRPC Initialized message).
    /// <paramref name="onDeviceUpdate"/> gets the device id of every device-tile update only (widget updates are ignored).</summary>
    Task<ILiveUpdateSubscription> Subscribe(string accessToken, string homeId, Action<string> onDeviceUpdate, CancellationToken ct);
}
