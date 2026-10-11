using Google.Protobuf.WellKnownTypes;
using Senswave.Contracts.Automations.V1;

namespace Senswave.Automations.EndTestsGrpcWeb;

[Trait("Collection", "EndTestGrpcWeb")]
public class AutomationsGrpcWebTests(GrpcWebTestEnvironment environment) : GrpcWebFeatureTest(environment)
{
    /// <summary>
    /// CreateAutomation → GetAutomation.
    /// </summary>
    [Fact]
    public async Task CreateThenGetAutomation()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new AutomationsService.AutomationsServiceClient(channel);
        var homeId = await channel.CreateHomeWithBrokerAsync();
        var deviceId = await channel.CreateDeviceAsync(homeId);
        var operationId = await channel.CreateBooleanOperationAsync(deviceId);

        // CreateAutomation
        var created = await client.CreateAutomationAsync(new CreateAutomationRequest
        {
            HomeId = homeId,
            Name = "Night mode",
            Icon = "moon",
            ConditionConnector = ConditionConnector.And,
            Conditions =
            {
                new ConditionDto
                {
                    OperationId = operationId,
                    ConditionType = ConditionType.Boolean,
                    ConditionConfiguration = Struct.Parser.ParseJson("""{ "isOn": true }"""),
                }
            },
            Results = { new ResultDto { OperationId = operationId, ValueToSend = Value.ForString(string.Empty) } },
        });

        // GetAutomation
        var automation = (await client.GetAutomationAsync(new GetAutomationRequest { AutomationId = created.Id })).Automation;

        Assert.Equal("Night mode", automation.Name);
        Assert.Equal(homeId, automation.HomeId);
        Assert.Single(automation.Conditions);
    }
}
