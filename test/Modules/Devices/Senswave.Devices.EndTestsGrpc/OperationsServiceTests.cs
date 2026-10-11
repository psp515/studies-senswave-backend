using Google.Protobuf.WellKnownTypes;
using Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class OperationsServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateOperation → GetOperation → DisplayOperations → DeleteOperation.
    /// </summary>
    [Fact]
    public async Task OperationLifecycle()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new OperationsService.OperationsServiceClient(channel);
        var deviceId = await channel.CreateDeviceAsync(await channel.CreateHomeWithBrokerAsync());
        var topic = Guid.NewGuid().ToString().Replace("-", "/");

        // CreateOperation
        var created = await client.CreateOperationAsync(new CreateOperationRequest
        {
            DeviceId = deviceId,
            Name = "Power",
            Type = "Boolean",
            Topic = topic,
            Configuration = Struct.Parser.ParseJson("""{ "jsonNames": ["value"], "isJson": true, "saveOnUserAction": true }"""),
        });

        // GetOperation + DisplayOperations
        var operation = await client.GetOperationAsync(new GetOperationRequest { OperationId = created.Id });
        var operations = await client.DisplayOperationsAsync(new DisplayOperationsRequest { DeviceId = deviceId });

        Assert.Equal("Power", operation.Name);
        Assert.Equal("Boolean", operation.Type);
        Assert.Equal(topic, operation.Topic);
        Assert.True(operation.Configuration.Fields["isJson"].BoolValue);
        Assert.Contains(operations.Items, x => x.Id == created.Id);

        // DeleteOperation
        await client.DeleteOperationAsync(new DeleteOperationRequest { OperationId = created.Id });
    }
}
