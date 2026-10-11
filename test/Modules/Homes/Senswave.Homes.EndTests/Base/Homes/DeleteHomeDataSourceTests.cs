using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.DataSources.Infrastructure;
using Senswave.Homes.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using System.Net;

namespace Senswave.Homes.EndTests.Base.Homes;

[Trait("Collection", "EndTest")]
public class DeleteHomeDataSourceTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var homeId = await PostHome();
        var response = await client.DeleteAsync($"{Paths.HomesPath}/{homeId}/datasource");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserCanDeleteDataSourceFromHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var brokerId = await PostBroker();
        var homeId = await PostHome();
        await PutBrokerForHome(brokerId, homeId);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.HomesPath}/{homeId}/datasource");
        var queryResponse = await client.GetAsync($"{Paths.HomesPath}/{homeId}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
    }

    [Fact]
    public async Task SubscribtionsAreCleared()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var broker = await PostBroker();
        var home = await PostHome();
        await PutBrokerForHome(broker, home);
        var device = await PostDevice(home);
        var operation = await PostBooleanOperation(device);
        using var scope = Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataSourcesContext>();

        // Act
        await AuthorizeClientAsUser(client);
        var removeOperation = await client.DeleteAsync($"{Paths.OperationsPath}/{operation}");
        var removeDevice = await client.DeleteAsync($"{Paths.DevicesPath}/{device}");
        var removeDataSource = await client.DeleteAsync($"{Paths.HomesPath}/{home}/datasource");
        var queryResponse = await client.GetAsync($"{Paths.HomesPath}/{home}");

        var subscribtionsCount = await context.Subscribtions
            .Where(x => x.BrokerId == broker)
            .CountAsync(default);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, removeOperation.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removeDevice.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, removeDataSource.StatusCode);
        Assert.Equal(HttpStatusCode.OK, queryResponse.StatusCode);
        Assert.Equal(0, subscribtionsCount);
    }

    [Fact]
    public async Task DataSourceReferenceIsRemoved()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var brokerId = await PostBroker();
        var homeId = await PostHome();
        await PutBrokerForHome(brokerId, homeId);

        var serviceProvider = Factory.Server.Services.GetService<IServiceProvider>()!;
        using var scope = serviceProvider.CreateScope();
        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.DeleteAsync($"{Paths.HomesPath}/{homeId}/datasource");

        var home = await scope.ServiceProvider
            .GetService<HomesContext>()!
            .Homes
            .FirstAsync(h => h.Id == homeId);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Null(home.DataSourceReference);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FriendCannotDeleteBrokerFromHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var brokerId = await PostBroker();
        var homeId = await PostHome();
        await PutBrokerForHome(brokerId, homeId);

        // Act
        await PrepareHomeSharingForAdmin(homeId, "Manage");
        await AuthorizeClientAsAdmin(client);

        var response = await client.DeleteAsync($"{Paths.HomesPath}/{homeId}/datasource");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
