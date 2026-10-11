using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.Devices.EndTests.Base.Widgets;

[Trait("Collection", "EndTest")]
public class ActionTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        var widgetId = await PostButtonWidget(operationId);

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

        // Act
        var response = await client.PostAsync($"{Paths.WidgetsPath}/{widgetId}/action", action);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task BrokerMustBeConnected()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        var widgetId = await PostButtonWidget(operationId);

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync($"{Paths.WidgetsPath}/{widgetId}/action", action);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MalciousCannotInvoke()
    {
        // Arrange
        var malcious = CreateUnauthorizedClient();
        var home = await PostHomeWithBroker();
        var deviceId = await PostDevice(home);
        var operationId = await PostBooleanOperation(deviceId);
        var widgetId = await PostButtonWidget(operationId);

        var action = RequestFactory.CreateActionRequest(value: "").Serialize();

        // Act
        await AuthorizeClientAsAdmin(malcious);
        var response = await malcious.PostAsync($"{Paths.WidgetsPath}/{widgetId}/action", action);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
