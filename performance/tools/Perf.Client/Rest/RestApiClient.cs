using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Perf.Client.Http;
using Perf.Core;
using Perf.Core.Runs;

namespace Perf.Client.Rest;

internal sealed class RestApiClient(ApiTarget target) : IApiClient
{
    internal const string PicoDeviceName = "Pico Controller";
    /// <summary>The seeded 10x4 dashboard S3 displays (falls back to the first dashboard when absent).</summary>
    internal const string LargeDashboardName = "Large";
    /// <summary>StartClientErrors.ClientIsRunning: the client of this broker is already started (500 ServerFail).</summary>
    internal const string ClientIsRunning = "ClientIsRunning";

    /// <summary>SignalR update type of a device tile change (DevicesUpdateService.DeviceTileUpdateType).</summary>
    private const string DeviceTileUpdateType = "deviceTileActionUpdate";

    private readonly HttpClient _http = HttpHandlers.Http2Client(target);
    private readonly HttpMessageInvoker _webSocketInvoker = new(HttpHandlers.Create(target));

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
        if (!body.Contains(ClientIsRunning, StringComparison.Ordinal))
            throw new HttpRequestException($"Starting broker client failed: {(int)response.StatusCode} {body}");
    }

    public async Task<UserData> Discover(string email, string accessToken, CancellationToken ct)
    {
        var homeId = (await Get("api/v1/homes/current", accessToken, ct))["id"]!.GetValue<string>();
        var devices = await Get($"api/v1/devices/display?homeId={homeId}&page=1&size=30", accessToken, ct);
        var deviceId = devices["items"]!.AsArray().First(d => d!["name"]!.GetValue<string>() == PicoDeviceName)!["id"]!.GetValue<string>();
        var dashboards = await Get($"api/v1/devices/dashboards/display?deviceId={deviceId}", accessToken, ct);
        var items = dashboards["items"]!.AsArray();
        var dashboardId = (items.FirstOrDefault(d => d!["name"]?.GetValue<string>() == LargeDashboardName) ?? items.First())!["id"]!.GetValue<string>();

        return new UserData(email, accessToken, homeId, deviceId, dashboardId);
    }

    public async Task TileAction(string accessToken, string deviceId, bool value, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"api/v1/devices/{deviceId}/tile/action") { Content = JsonContent.Create(new { value }) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<ILiveUpdateSubscription> Subscribe(string accessToken, string homeId, Action<string> onDeviceUpdate, CancellationToken ct,
        Action<JsonNode, int>? onRawUpdate = null)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(target.BaseUri, "signalr/liveupdates/live"), options =>
            {
                options.AccessTokenProvider = () => Task.FromResult<string?>(accessToken);
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.WebSocketFactory = async (context, token) =>
                {
                    // The default factory cannot do WebSockets over HTTP/2: ConnectAsync needs an HttpMessageInvoker.
                    var socket = new ClientWebSocket();
                    socket.Options.HttpVersion = System.Net.HttpVersion.Version20;
                    socket.Options.HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact;
                    socket.Options.SetRequestHeader("Authorization", $"Bearer {accessToken}");
                    try
                    {
                        await socket.ConnectAsync(context.Uri, _webSocketInvoker, token);
                    }
                    catch
                    {
                        socket.Dispose();
                        throw;
                    }
                    return socket;
                };
            })
            .Build();

        var subscription = new Subscription(connection);
        connection.Closed += _ =>
        {
            subscription.MarkClosed();
            return Task.CompletedTask;
        };

        var initialized = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On("Initialized", () => initialized.TrySetResult());
        connection.On<string>("FailedToInitialize", reason => initialized.TrySetException(new InvalidOperationException(reason)));
        connection.On<string, JsonNode>("Update", (updateType, data) =>
        {
            if (onRawUpdate is not null)
            {
                // The hub invocation as it travels in the SignalR JSON protocol (without the record separator).
                var invocation = new JsonObject { ["type"] = 1, ["target"] = "Update", ["arguments"] = new JsonArray(updateType, data?.DeepClone()) };
                onRawUpdate(invocation, System.Text.Encoding.UTF8.GetByteCount(invocation.ToJsonString()));
            }

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

        return subscription;
    }

    public async Task<ResponseSample> Sample(Scenario scenario, UserData user, CancellationToken ct)
    {
        if (scenario.IsStream())
            return await StreamSample.Record(this, scenario, Transport.Rest, user, ct);

        var (method, path, body) = scenario switch
        {
            Scenario.S1 => (HttpMethod.Get, "api/v1/users", (object?)null),
            Scenario.S2 => (HttpMethod.Get, $"api/v1/devices/operations/display?deviceId={user.DeviceId}&page=1&size=30", null),
            Scenario.S3 => (HttpMethod.Get, $"api/v1/devices/dashboards/{user.DashboardId}/display", null),
            Scenario.S4 => (HttpMethod.Get, "api/v1/legal/terms", null),
            Scenario.S5 => (HttpMethod.Post, $"api/v1/devices/{user.DeviceId}/tile/action", new { value = true }),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        using var request = new HttpRequestMessage(method, path);
        if (scenario != Scenario.S4)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.AccessToken);
        if (body is not null)
            request.Content = JsonContent.Create(body);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        var bytes = await response.Content.ReadAsByteArrayAsync(ct);
        var json = bytes.Length == 0 ? null : JsonNode.Parse(bytes);

        return new ResponseSample(scenario.ToString(), Transport.Rest.ToString(), $"{method} /{path}", "json", bytes.Length, json);
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
        _webSocketInvoker.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription(HubConnection connection) : ILiveUpdateSubscription
    {
        private volatile bool _disposing;
        private volatile bool _ended;

        public bool Ended => _ended;

        public void MarkClosed()
        {
            if (!_disposing)
                _ended = true;
        }

        public async ValueTask DisposeAsync()
        {
            _disposing = true;
            try
            {
                await connection.DisposeAsync();
            }
            catch
            {
                // teardown must never throw
            }
        }
    }
}
