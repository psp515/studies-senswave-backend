using Senswave.TestInfrastructure.Fixtures.Mqtt;

namespace Senswave.TestInfrastructure.TestEnvironments.Grpc;

/// <summary>
/// <see cref="GrpcTestEnvironment"/> plus a Mosquitto broker, for rpcs that need a live MQTT connection
/// (starting, restarting and stopping broker clients).
/// </summary>
public class GrpcMqttTestEnvironment : GrpcTestEnvironment
{
    internal static readonly IMqttFixture MqttFixture = new MosquittoContainerFixture();

    public IMqttFixture GetMqttProvider() => MqttFixture;

    public override async Task InitializeAsync()
    {
        await MqttFixture.InitializeAsync();

        if (!MqttFixture.IsWorking())
        {
            //If running locally start mosquitto container or change running to release
            throw new InvalidOperationException("Mqtt container is not running.");
        }

        await base.InitializeAsync();
    }
}
