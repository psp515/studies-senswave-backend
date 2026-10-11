using Google.Protobuf.WellKnownTypes;
using Senswave.Contracts.Automations.V1;

namespace Senswave.Automations.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class AutomationsServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateAutomation → GetAutomation → DisplayAutomations → UpdateAutomation → AutomationState → PutConditionToAutomation
    /// → PutResultToAutomation → DeleteCondition → DeleteResult → DeleteAutomation.
    /// </summary>
    [Fact]
    public async Task AutomationLifecycle()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new AutomationsService.AutomationsServiceClient(channel);
        var homeId = await channel.CreateHomeWithBrokerAsync();
        var deviceId = await channel.CreateDeviceAsync(homeId);
        var conditionOperationId = await channel.CreateBooleanOperationAsync(deviceId);
        var resultOperationId = await channel.CreateBooleanOperationAsync(deviceId);

        // CreateAutomation
        var created = await client.CreateAutomationAsync(new CreateAutomationRequest
        {
            HomeId = homeId,
            Name = "Night mode",
            Icon = "moon",
            ConditionConnector = ConditionConnector.And,
            Conditions = { Condition(conditionOperationId) },
            Results = { new ResultDto { OperationId = resultOperationId, ValueToSend = Value.ForString(string.Empty) } },
        });
        var automationId = created.Id;

        // GetAutomation + DisplayAutomations
        var automation = (await client.GetAutomationAsync(new GetAutomationRequest { AutomationId = automationId })).Automation;
        var automations = await client.DisplayAutomationsAsync(new DisplayAutomationsRequest { HomeId = homeId });

        Assert.Equal("Night mode", automation.Name);
        Assert.Equal(homeId, automation.HomeId);
        Assert.Equal(ConditionConnector.And, automation.ConditionConnector);
        Assert.Equal(ConditionType.Boolean, Assert.Single(automation.Conditions).ConditionType);
        Assert.Single(automation.Results);
        Assert.Contains(automations.Items, x => x.Id == automationId);

        // UpdateAutomation + AutomationState
        await client.UpdateAutomationAsync(new UpdateAutomationRequest
        {
            AutomationId = automationId,
            Name = "Evening mode",
            Icon = "moon",
            ConditionConnector = ConditionConnector.Or,
        });
        await client.AutomationStateAsync(new AutomationStateRequest { AutomationId = automationId, IsEnabled = !automation.IsEnabled });

        var updated = (await client.GetAutomationAsync(new GetAutomationRequest { AutomationId = automationId })).Automation;

        Assert.Equal("Evening mode", updated.Name);
        Assert.Equal(!automation.IsEnabled, updated.IsEnabled);

        // PutConditionToAutomation + PutResultToAutomation (a second of each, so one can be deleted)
        await client.PutConditionToAutomationAsync(new PutConditionToAutomationRequest
        {
            AutomationId = automationId,
            OperationId = resultOperationId,
            ConditionType = ConditionType.Boolean,
            ConditionConfiguration = Struct.Parser.ParseJson("""{ "isOn": false }"""),
        });
        await client.PutResultToAutomationAsync(new PutResultToAutomationRequest
        {
            AutomationId = automationId,
            OperationId = conditionOperationId,
            Value = "true",
        });

        var extended = (await client.GetAutomationAsync(new GetAutomationRequest { AutomationId = automationId })).Automation;

        Assert.Equal(2, extended.Conditions.Count);
        Assert.Equal(2, extended.Results.Count);

        // DeleteCondition + DeleteResult + DeleteAutomation
        await client.DeleteConditionAsync(new DeleteConditionRequest { ConditionId = extended.Conditions[0].Id });
        await client.DeleteResultAsync(new DeleteResultRequest { ResultId = extended.Results[0].Id });
        await client.DeleteAutomationAsync(new DeleteAutomationRequest { AutomationId = automationId });
    }

    private static ConditionDto Condition(string operationId) => new()
    {
        OperationId = operationId,
        ConditionType = ConditionType.Boolean,
        ConditionConfiguration = Struct.Parser.ParseJson("""{ "isOn": true }"""),
    };
}
