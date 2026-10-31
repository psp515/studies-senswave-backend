using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Perf.Client.Http;
using Perf.Core.Runs;

namespace Perf.Client.Rest;

internal sealed class RestApiClient(ApiTarget target) : IApiClient
{
    internal const string PicoDeviceName = "Pico Controller";
    internal const string ConnectionAlreadyExists = "ConnectionAlreadyExists";

    /// <summary>SignalR update type of a device tile change (DevicesUpdateService.DeviceTileUpdateType).</summary>
    private const string DeviceTileUpdateType = "deviceTileActionUpdate";

    private readonly HttpClient _http = HttpHandlers.Http2Client(target);

    public async Task<string> Login(string email, string password, CancellationToken ct)
    {
        using var response = await _http.PostAsJsonAsync("api/v1/auth/login", new { email, password }, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>(ct))!["accessToken"]!.GetValue<string>();
    }

    public async Task StartBrokerClient(string accessToken, string username, string password, CancellationToken ct)
    {
        var homeId = (await Get("api/v1/homes/current", accessToken, ct))["id"]!.GetValue<string>();
        var homes = await Get("api/v1/homes", accessToken, ct);
        var dataSourceId = homes["items"]!.AsArray().First(h => h!["id"]!.GetValue<string>() == homeId)!["dataSourceId"]?.GetValue<string>()
            ?? throw new InvalidOperationException($"Home {homeId} has no data source.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/datasources/brokers/{dataSourceId}/clients")
        {
            Content = JsonContent.Create(new { username, password }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!body.Contains(ConnectionAlreadyExists, StringComparison.Ordinal))
            throw new HttpRequestException($"Starting broker client failed: {(int)response.StatusCode} {body}");
    }

    public async Task<UserData> Discover(string email, string accessToken, CancellationToken ct)
    {
        var homeId = (await Get("api/v1/homes/current", accessToken, ct))["id"]!.GetValue<string>();
        var devices = await Get($"api/v1/devices/display?homeId={homeId}&page=1&size=30", accessToken, ct);
        var deviceId = devices["items"]!.AsArray().First(d => d!["name"]!.GetValue<string>() == PicoDeviceName)!["id"]!.GetValue<string>();
        var dashboards = await Get($"api/v1/devices/dashboards/display?deviceId={deviceId}", accessToken, ct);
        var dashboardId = dashboards["items"]!.AsArray().First()!["id"]!.GetValue<string>();

        return new UserData(email, accessToken, homeId, deviceId, dashboardId);
    }

    public async Task TileAction(string accessToken, string deviceId, bool value, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/devices/{deviceId}/tile/action") { Content = JsonContent.Create(new { value }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ILiveUpdateSubscription> Subscribe(string accessToken, string homeId, Action<string> onDeviceUpdate, CancellationToken ct)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(target.BaseUri, "signalr/liveupdates/live"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.HttpMessageHandlerFactory = _ => HttpHandlers.Create(target);
                options.WebSocketConfiguration = ws =>
                {
                    ws.HttpVersion = System.Net.HttpVersion.Version20;
                    ws.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                    if (target.InsecureTls)
                        ws.RemoteCertificateValidationCallback = (_, _, _, _) => true;
                };
            })
            .Build();

        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Initialized", () => initialized.TrySetResult());
        connection.On<string>("FailedToInitialize", reason => initialized.TrySetException(new InvalidOperationException(reason)));
        connection.On<string, JsonNode>("Update", (updateType, data) =>
        {
            if (updateType == DeviceTileUpdateType && data?["deviceId"]?.GetValue<string>() is { } deviceId)
                onDeviceUpdate(deviceId);
        });

        try
        {
            await connection.StartAsync(ct);
            await connection.InvokeAsync("Initialize", homeId, ct);
            await initialized.Task.WaitAsync(TimeSpan.FromSeconds(15), ct);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        return new Subscription(connection);
    }

    private async Task<JsonObject> Get(string path, string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonObject>(ct))!;
    }

    public ValueTask DisposeAsync()
    {
        _http.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription(HubConnection connection) : ILiveUpdateSubscription
    {
        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
