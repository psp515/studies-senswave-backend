using System.Text.Json.Nodes;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Perf.Core;
using Perf.Client.Http;
using Perf.Core.Runs;
using Senswave.Contracts.DataSources.V1;
using Senswave.Contracts.Devices.V1;
using Senswave.Contracts.Homes.V1;
using Senswave.Contracts.LiveUpdates.V1;
using Senswave.Contracts.Users.V1;

namespace Perf.Client.Grpc;

/// <summary>Native gRPC or gRPC-Web (binary <c>application/grpc-web+proto</c>), both over HTTP/2.</summary>
internal sealed class GrpcApiClient : IApiClient
{
    private readonly GrpcChannel _channel;
    private readonly Transport _transport;

    public GrpcApiClient(ApiTarget target, bool web)
    {
        _transport = web ? Transport.GrpcWeb : Transport.Grpc;
        HttpMessageHandler handler = HttpHandlers.Create(target);
        if (web)
            handler = new GrpcWebHandler(GrpcWebMode.GrpcWeb, handler);

        _channel = GrpcChannel.ForAddress(target.BaseUri, new GrpcChannelOptions { HttpHandler = handler, MaxReceiveMessageSize = 16 * 1024 * 1024, HttpVersion = System.Net.HttpVersion.Version20, HttpVersionPolicy = HttpVersionPolicy.RequestVersionExact });
    }

    private static Metadata Auth(string token) => new() { { "Authorization", $"Bearer {token}" } };

    public async Task<string> Login(string email, string password, CancellationToken ct) =>
        (await new AuthService.AuthServiceClient(_channel).LoginAsync(new LoginRequest { Email = email, Password = password }, cancellationToken: ct)).AccessToken;

    public async Task StartBrokerClient(string accessToken, string username, string password, CancellationToken ct)
    {
        var homes = new HomesService.HomesServiceClient(_channel);
        var homeId = (await homes.GetCurrentHomeAsync(new GetCurrentHomeRequest(), Auth(accessToken), cancellationToken: ct)).Id;
        var list = await homes.GetHomesAsync(new GetHomesRequest { Page = 1, Size = 30 }, Auth(accessToken), cancellationToken: ct);
        var home = list.Items.First(h => h.Id == homeId);
        if (!home.HasDataSourceId)
            throw new InvalidOperationException($"Home {homeId} has no data source.");

        var dataSources = new DataSourcesService.DataSourcesServiceClient(_channel);
        try
        {
            await dataSources.StartClientAsync(
                new StartClientRequest { BrokerId = home.DataSourceId, Username = username, Password = password }, Auth(accessToken), cancellationToken: ct);
        }
        catch (RpcException e) when (e.StatusCode == StatusCode.Internal)
        {
            if (!await ClientIsRunning(dataSources, home.DataSourceId, accessToken, ct))
                throw;
        }
    }

    /// <summary>An already started client is reported as a generic Internal error ("Server Error"), so the state is checked instead.</summary>
    private static async Task<bool> ClientIsRunning(DataSourcesService.DataSourcesServiceClient dataSources, string brokerId, string accessToken, CancellationToken ct)
    {
        try
        {
            await dataSources.GetClientStateAsync(new GetClientStateRequest { BrokerId = brokerId }, Auth(accessToken), cancellationToken: ct);
            return true;
        }
        catch (RpcException)
        {
            return false;
        }
    }

    public async Task<UserData> Discover(string email, string accessToken, CancellationToken ct)
    {
        var homeId = (await new HomesService.HomesServiceClient(_channel).GetCurrentHomeAsync(new GetCurrentHomeRequest(), Auth(accessToken), cancellationToken: ct)).Id;
        var devices = await new DevicesService.DevicesServiceClient(_channel).DisplayDevicesAsync(
            new DisplayDevicesRequest { HomeId = homeId, Page = 1, Size = 30 }, Auth(accessToken), cancellationToken: ct);
        var deviceId = devices.Items.First(d => d.Name == Rest.RestApiClient.PicoDeviceName).Id;
        var dashboards = await new DashboardsService.DashboardsServiceClient(_channel).DisplayDashboardsAsync(
            new DisplayDashboardsRequest { DeviceId = deviceId }, Auth(accessToken), cancellationToken: ct);

        var dashboard = dashboards.Items.FirstOrDefault(d => d.Name == Rest.RestApiClient.LargeDashboardName) ?? dashboards.Items.First();
        return new UserData(email, accessToken, homeId, deviceId, dashboard.Id);
    }

    public async Task TileAction(string accessToken, string deviceId, bool value, CancellationToken ct) =>
        await new DevicesService.DevicesServiceClient(_channel).DeviceTileActionAsync(
            new DeviceTileActionRequest { DeviceId = deviceId, Value = Value.ForBool(value) }, Auth(accessToken), cancellationToken: ct);

    public async Task<ILiveUpdateSubscription> Subscribe(string accessToken, string homeId, Action<string> onDeviceUpdate, CancellationToken ct,
        Action<JsonNode, int>? onRawUpdate = null)
    {
        var lifetime = new CancellationTokenSource();
        var call = new LiveUpdatesService.LiveUpdatesServiceClient(_channel)
            .Subscribe(new SubscribeRequest { HomeReferenceId = homeId }, Auth(accessToken), cancellationToken: lifetime.Token);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            if (!await call.ResponseStream.MoveNext(timeout.Token) || call.ResponseStream.Current.UpdateCase != LiveUpdate.UpdateOneofCase.Initialized)
                throw new InvalidOperationException("Live-update stream did not start with Initialized.");
        }
        catch
        {
            await lifetime.CancelAsync();
            call.Dispose();
            lifetime.Dispose();
            throw;
        }

        var subscription = new Subscription(call, lifetime);
        subscription.Start(onDeviceUpdate, onRawUpdate);
        return subscription;
    }

    public async Task<ResponseSample> Sample(Scenario scenario, UserData user, CancellationToken ct)
    {
        if (scenario.IsStream())
            return await StreamSample.Record(this, scenario, _transport, user, ct);

        var auth = Auth(user.AccessToken);
        (string Call, IMessage Message) result = scenario switch
        {
            Scenario.S1 => ("senswave.users.v1.UsersService/GetUser",
                await new UsersService.UsersServiceClient(_channel).GetUserAsync(new GetUserRequest(), auth, cancellationToken: ct)),
            Scenario.S2 => ("senswave.devices.v1.OperationsService/DisplayOperations",
                await new OperationsService.OperationsServiceClient(_channel).DisplayOperationsAsync(
                    new DisplayOperationsRequest { DeviceId = user.DeviceId, Page = 1, Size = 30 }, auth, cancellationToken: ct)),
            Scenario.S3 => ("senswave.devices.v1.DashboardsService/DisplayDashboard",
                await new DashboardsService.DashboardsServiceClient(_channel).DisplayDashboardAsync(
                    new DisplayDashboardRequest { DashboardId = user.DashboardId }, auth, cancellationToken: ct)),
            Scenario.S4 => ("senswave.users.v1.LegalService/GetTermsAndConditions",
                await new LegalService.LegalServiceClient(_channel).GetTermsAndConditionsAsync(new GetTermsAndConditionsRequest(), cancellationToken: ct)),
            Scenario.S5 => ("senswave.devices.v1.DevicesService/DeviceTileAction",
                await new DevicesService.DevicesServiceClient(_channel).DeviceTileActionAsync(
                    new DeviceTileActionRequest { DeviceId = user.DeviceId, Value = Value.ForBool(true) }, auth, cancellationToken: ct)),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

        return new ResponseSample(scenario.ToString(), _transport.ToString(), result.Call, "protobuf", result.Message.CalculateSize(), ToJson(result.Message));
    }

    /// <summary>Protobuf message as JSON, including fields with default values (proto3 does not send them on the wire).</summary>
    private static JsonNode ToJson(IMessage message) =>
        JsonNode.Parse(new JsonFormatter(JsonFormatter.Settings.Default.WithFormatDefaultValues(true)).Format(message))!;

    public ValueTask DisposeAsync()
    {
        _channel.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription(AsyncServerStreamingCall<LiveUpdate> call, CancellationTokenSource lifetime) : ILiveUpdateSubscription
    {
        private Task _reader = Task.CompletedTask;
        private volatile bool _ended;

        public bool Ended => _ended;

        public void Start(Action<string> onDeviceUpdate, Action<JsonNode, int>? onRawUpdate) => _reader = Task.Run(async () =>
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lifetime.Token))
                {
                    var update = call.ResponseStream.Current;
                    if (onRawUpdate is not null)
                    {
                        try { onRawUpdate(ToJson(update), update.CalculateSize()); } catch { /* sampling must not kill the reader */ }
                    }

                    if (update.UpdateCase != LiveUpdate.UpdateOneofCase.DeviceTile)
                        continue;

                    try
                    {
                        onDeviceUpdate(update.DeviceTile.DeviceId);
                    }
                    catch
                    {
                        // a throwing callback must not kill the reader
                    }
                }
            }
            catch
            {
                // stream fault or cancellation: reflected in Ended below
            }
            finally
            {
                if (!lifetime.IsCancellationRequested)
                    _ended = true;
            }
        });

        public async ValueTask DisposeAsync()
        {
            try
            {
                await lifetime.CancelAsync();
                await _reader;
            }
            catch
            {
                // teardown must never throw
            }
            finally
            {
                call.Dispose();
                lifetime.Dispose();
            }
        }
    }
}
