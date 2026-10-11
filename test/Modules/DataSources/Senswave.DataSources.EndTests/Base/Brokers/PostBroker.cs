using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.BrokerConnection.Interfaces;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.DataSources.EndTests.Base.Brokers;

[Trait("Collection", "EndTest")]
public class PostBroker(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Fact]
    public async Task PostBrokerDoesNotCreateConnection()
    {
        // Arrange
        var connectionService = Services.GetRequiredService<IClientService>();

        // Act
        var id = await PostBroker();

        var service = connectionService!.GetClient(id);

        // Assert
        Assert.True(service.IsFailure);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        var request = RequestFactory.CreatePostBrokerRequest()
            .Serialize();

        // Act
        var response = await client.PostAsync(Paths.BrokersPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IsValidationEnabled()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: "a",
            url: "&*((x@#!$@!#%@#5",
            clientName: "Seed Connection",
            port: 1883,
            protocolVersion: "MqttV5").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.BrokersPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IsValidationEnabledProtocolVersion()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            url: Guid.NewGuid().ToString(),
            clientName: "Seed Connection",
            port: 1883,
            protocolVersion: "sdfg").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.BrokersPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task NameAlreadyUsed()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            clientName: "Seed Connection",
            url: Guid.NewGuid().ToString(),
            port: 1234,
            protocolVersion: "MqttV5").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var creationResponse = await client.PostAsync(Paths.BrokersPath, request);
        var response = await client.PostAsync(Paths.BrokersPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, creationResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UrlAndPortAlreadyUsed()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var client2 = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            clientName: "Seed Connection",
            url: Guid.NewGuid().ToString(),
            port: 1234,
            protocolVersion: "MqttV5").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.BrokersPath, request);
        await AuthorizeClientAsAdmin(client2);
        var response2 = await client2.PostAsync(Paths.BrokersPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, response2.StatusCode);
    }

    [Fact]
    public async Task UserCanCreateBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            clientName: "Seed Connection",
            url: Guid.NewGuid().ToString(),
            port: 1234,
            protocolVersion: "MqttV311").Serialize();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.BrokersPath, request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task CannotCreateDuplicateBroker()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePostBrokerRequest(
            name: Guid.NewGuid().ToString(),
            clientName: "Seed Connection",
            url: Guid.NewGuid().ToString(),
            port: 1234,
            protocolVersion: "MqttV5");

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.BrokersPath, request.Serialize());

        await AuthorizeClientAsAdmin(client);
        request["name"] = Guid.NewGuid().ToString();
        var failResponse = await client.PostAsync(Paths.BrokersPath, request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, failResponse.StatusCode);
    }
}
