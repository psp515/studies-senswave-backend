using Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.EndTestsGrpc;

[Trait("Collection", "MqttEndTestGrpc")]
public class BrokerClientServiceTests(GrpcMqttTestEnvironment environment) : GrpcMqttFeatureTest(environment)
{
    /// <summary>
    /// StartClient → StopClient against a live Mosquitto broker.
    /// RestartClient is not covered: it only succeeds for a client that lost its connection
    /// (connected → AlreadyConnected, stopped → AlreadyDisconnected), which a success scenario cannot reach.
    /// </summary>
    [Fact]
    public async Task ClientLifecycle()
    {
        // Arrange
        var mqtt = MqttEnvironment.GetMqttProvider();
        var client = await CreateAuthorizedClientAsUser<DataSourcesService.DataSourcesServiceClient>();

        var broker = await client.CreateBrokerAsync(new CreateBrokerRequest
        {
            Name = "Mqtt Broker",
            Url = mqtt.Hostname,
            ClientName = Guid.NewGuid().ToString(),
            Port = mqtt.Port,
            ProtocolVersion = mqtt.Version,
            UseTls = mqtt.UseTls,
            Username = mqtt.Username,
            Password = mqtt.Password,
        });

        // StartClient + StopClient
        await client.StartClientAsync(new StartClientRequest { BrokerId = broker.Id, Username = mqtt.Username, Password = mqtt.Password });

        var state = await client.GetClientStateAsync(new GetClientStateRequest { BrokerId = broker.Id });
        Assert.NotEqual("NotStarted", state.ConnectionStatus);

        await client.StopClientAsync(new StopClientRequest { BrokerId = broker.Id });
    }
}
