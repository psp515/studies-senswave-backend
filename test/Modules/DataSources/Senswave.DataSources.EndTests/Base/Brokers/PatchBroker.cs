using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using Senswave.TestInfrastructure.TestSetup.Models.Common;

namespace Senswave.DataSources.EndTests.Base.Brokers;

[Trait("Collection", "EndTest")]
public class PatchBroker(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchBrokerRequest().Serialize();

        // Act
        var id = await PostBroker();
        var response = await client.PatchAsync($"{Paths.BrokersPath}/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanUpdateHisBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchBrokerRequest(
            name: Guid.NewGuid().ToString()).Serialize();

        // Act
        var id = await PostBroker();

        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.BrokersPath}/{id}", request);
        var content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task InvalidProtocolForBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchBrokerRequest(
            protocolVersion: "protocol").Serialize();

        // Act
        var id = await PostBroker();

        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.BrokersPath}/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationIsWorking()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchBrokerRequest(
            protocolVersion: "protocol",
            url: "4#@#%$$#%#$%#$^#$)(*&^_)#$MJDFG123url.com").Serialize();

        // Act
        var id = await PostBroker();

        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.BrokersPath}/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotUpdateNotHisBroker()
    {
        // Arrange
        var request = RequestFactory.CreatePatchBrokerRequest(
            name: Guid.NewGuid().ToString()[..29]).Serialize();

        var stealer = CreateUnauthorizedClient();

        // Act
        var id = await PostBroker();
        await AuthorizeClientAsAdmin(stealer);
        var stealerResponse = await stealer.PatchAsync($"{Paths.BrokersPath}/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, stealerResponse.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UsersCannotPatchBrokersWithSameUrlAndPort()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var url = Guid.NewGuid().ToString();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            clientName: "clientName",
            url: url,
            port: 31777,
            protocolVersion: "MqttV5");

        var client2 = CreateUnauthorizedClient();
        var request2 = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            clientName: "clientName",
            url: Guid.NewGuid().ToString(),
            port: 31777,
            protocolVersion: "MqttV5");

        var patchRequest = RequestFactory.CreatePatchBrokerRequest(url: url).Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var userReponse = await client.PostAsync(Paths.BrokersPath, request.Serialize());

        await AuthorizeClientAsAdmin(client2);
        var adminResponse = await client2.PostAsync(Paths.BrokersPath, request2.Serialize());
        var stringResponse = await adminResponse.Content.ReadAsStringAsync();
        var idResponse = JsonSerializer.Deserialize<IdResponse>(stringResponse);

        var failResponse = await client2.PatchAsync($"{Paths.BrokersPath}/{idResponse!.Id}", patchRequest);

        // Assert
        Assert.Equal(HttpStatusCode.Created, userReponse.StatusCode);
        Assert.Equal(HttpStatusCode.Created, adminResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, failResponse.StatusCode);
    }
}
