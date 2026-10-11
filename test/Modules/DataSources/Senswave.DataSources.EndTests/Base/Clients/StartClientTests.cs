using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;

namespace Senswave.DataSources.EndTests.Base.Clients;

[Trait("Collection", "EndTest")]
public class StartClientTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreateStartClient()
            .Serialize();

        // Act
        var brokerId = await PostBroker();
        var response = await client.PostAsync(Paths.ClientsPath(brokerId), request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task RandomUserCanNotStartConnection()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreateStartClient(
            username: "TestTest123!",
            password: "TestTest123!").Serialize();

        // Act
        var brokerId = await PostBroker();
        await AuthorizeClientAsAdmin(client);
        var response = await client.PostAsync(Paths.ClientsPath(brokerId), request);

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorks()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreateStartClient().Serialize();

        // Act
        var brokerId = await PostBroker();
        await AuthorizeClientAsUser(client);
        var response = await client.PostAsync(Paths.ClientsPath(brokerId), request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
