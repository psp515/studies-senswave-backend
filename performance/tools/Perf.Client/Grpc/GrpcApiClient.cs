using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
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

    public GrpcApiClient(ApiTarget target, bool web)
    {
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

        return new UserData(email, accessToken, homeId, deviceId, dashboards.Items.First().Id);
    }

    public async Task TileAction(string accessToken, string deviceId, bool value, CancellationToken ct) =>
        await new DevicesService.DevicesServiceClient(_channel).DeviceTileActionAsync(
            new DeviceTileActionRequest { DeviceId = deviceId, Value = Value.ForBool(value) }, Auth(accessToken), cancellationToken: ct);

    public async Task<ILiveUpdateSubscription> Subscribe(string accessToken, string homeId, Action<string> onDeviceUpdate, CancellationToken ct)
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

        var reader = Task.Run(async () =>
        {
            try
            {
                while (await call.ResponseStream.MoveNext(lifetime.Token))
                {
                    var update = call.ResponseStream.Current;
                    if (update.UpdateCase == LiveUpdate.UpdateOneofCase.DeviceTile)
                        onDeviceUpdate(update.DeviceTile.DeviceId);
                }
            }
            catch (RpcException) when (lifetime.IsCancellationRequested) { }
            catch (OperationCanceledException) { }
        });

        return new Subscription(call, lifetime, reader);
    }

    public ValueTask DisposeAsync()
    {
        _channel.Dispose();
        return ValueTask.CompletedTask;
    }

    private sealed class Subscription(AsyncServerStreamingCall<LiveUpdate> call, CancellationTokenSource lifetime, Task reader) : ILiveUpdateSubscription
    {
        public async ValueTask DisposeAsync()
        {
            await lifetime.CancelAsync();
            await reader;
            call.Dispose();
            lifetime.Dispose();
        }
    }
}
