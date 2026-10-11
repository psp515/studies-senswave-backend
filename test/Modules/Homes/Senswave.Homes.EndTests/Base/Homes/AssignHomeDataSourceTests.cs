using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Senswave.Homes.EndTests.Base.Homes;

[Trait("Collection", "EndTest")]
public class AssignHomeDataSourceTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePutHomeBroker(Guid.NewGuid()).Serialize();

        // Act
        var homeId = await PostHome();
        var response = await client.PutAsync($"{Paths.HomesPath}/{homeId}/datasource", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorks()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePutHomeBroker(Guid.NewGuid()).Serialize();

        // Act
        var homeId = await PostHome();
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.HomesPath}/{homeId}/datasource", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserCanPutBrokerForHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var brokerId = await PostBroker();
        var homeId = await PostHome();
        var request = RequestFactory.CreatePutHomeBroker(brokerId).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.HomesPath}/{homeId}/datasource", request);
        var response1 = await client.GetAsync($"{Paths.HomesPath}/{homeId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);

        var content = await response1.Content.ReadAsStringAsync();
        var home = JsonSerializer.Deserialize<JsonObject>(content);

        Assert.NotNull(home);
        Assert.Equal(brokerId.ToString(), home["dataSource"]!["id"]!.ToString());
        Assert.Equal("NotStarted", home["dataSource"]!["state"]!.ToString());
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FriendCannotPutBrokerForHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var brokerId = await PostBroker();
        var request = RequestFactory.CreatePutHomeBroker(brokerId).Serialize();

        // Act
        var homeId = await PostHome();
        await PrepareHomeSharingForAdmin(homeId, "Manage");
        await AuthorizeClientAsAdmin(client);
        var response = await client.PutAsync($"{Paths.HomesPath}/{homeId}/datasource", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotChangeDataSourceForHomeIfDevicesAvailable()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var homeId = await PostHome();
        await PutBrokerForHome(homeId);
        var brokerId = await PostBroker();
        var request = RequestFactory.CreatePutHomeBroker(brokerId).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.HomesPath}/{homeId}/datasource", request);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotReassignNewDataSourceIfAlreadyAssigned()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var homeId = await PostHome();
        var broker = await PostBroker();
        var newBroker = await PostBroker();
        var request = RequestFactory.CreatePutHomeBroker(broker).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PutAsync($"{Paths.HomesPath}/{homeId}/datasource", request);

        var newBrokerRequest = RequestFactory.CreatePutHomeBroker(newBroker).Serialize();
        var overrideResponse = await client.PutAsync($"{Paths.HomesPath}/{homeId}/datasource", newBrokerRequest);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, overrideResponse.StatusCode);
    }
}
