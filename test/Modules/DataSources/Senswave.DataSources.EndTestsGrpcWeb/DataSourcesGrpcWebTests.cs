using Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.EndTestsGrpcWeb;

[Trait("Collection", "EndTestGrpcWeb")]
public class DataSourcesGrpcWebTests(GrpcWebTestEnvironment environment) : GrpcWebFeatureTest(environment)
{
    /// <summary>
    /// CreateBroker → GetBroker.
    /// </summary>
    [Fact]
    public async Task CreateThenGetBroker()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new DataSourcesService.DataSourcesServiceClient(channel);

        // CreateBroker
        var brokerId = await channel.CreateBrokerAsync();

        // GetBroker
        var broker = await client.GetBrokerAsync(new GetBrokerRequest { BrokerId = brokerId });

        Assert.Equal(brokerId, broker.Id);
        Assert.Equal("MqttV5", broker.ProtocolVersion);
    }
}
