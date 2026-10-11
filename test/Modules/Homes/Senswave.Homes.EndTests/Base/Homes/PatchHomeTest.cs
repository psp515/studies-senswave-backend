using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Homes.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Net;

namespace Senswave.Homes.EndTests.Base.Homes;

[Trait("Collection", "EndTest")]
public class PatchHomeTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchHome().Serialize();

        // Act
        var id = await PostHome();
        var path = $"{Paths.HomesPath}/{id}";
        var response = await client.PatchAsync(path, request);

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserMayUpdateHisHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchHome(name: Guid.NewGuid().ToString()).Serialize();

        // Act
        var id = await PostHome();
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.HomesPath}/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task UpdateSkipsDuplicateName()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var name = Guid.NewGuid().ToString();
        var request = RequestFactory.CreatePatchHome(name: name).Serialize();
        var duplicateRequest = RequestFactory.CreatePatchHome(name: name).Serialize();

        var home = await PostHome();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.HomesPath}/{home}", request);
        var duplicateResponse = await client.PatchAsync($"{Paths.HomesPath}/{home}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task UserMayUpdateLocalization()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        double longitude = 1;
        double latitude = 1;
        var request = RequestFactory.CreatePatchHome(
            name: Guid.NewGuid().ToString(),
            longitude: longitude,
            latitude: latitude).Serialize();

        var serviceProvider = Factory.Server.Services.GetRequiredService<IServiceProvider>();
        using var scope = serviceProvider.CreateScope();

        // Act
        var id = await PostHome();
        await AuthorizeClientAsUser(client);
        var response = await client.PatchAsync($"{Paths.HomesPath}/{id}", request);

        var homeWithLocation = await scope.ServiceProvider
            .GetRequiredService<HomesContext>()
            .Homes
            .Include(x => x.Location)
            .FirstOrDefaultAsync(x => x.Id == id);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(longitude, homeWithLocation!.Location!.Longitude);
        Assert.Equal(latitude, homeWithLocation.Location!.Latitude);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotUpdateNotHisHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var fraud = CreateUnauthorizedClient();
        var request = RequestFactory.CreatePatchHome(name: Guid.NewGuid().ToString()).Serialize();

        // Act
        var id = await PostHome();

        await AuthorizeClientAsUser(client);
        await AuthorizeClientAsAdmin(fraud);

        var response = await fraud.PatchAsync($"{Paths.HomesPath}/{id}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorking()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var fraud = CreateUnauthorizedClient();
        var wrongNameRequest = RequestFactory.CreatePatchHome(name: new string('a', 120)).Serialize();
        var wrongLongitudeRequest = RequestFactory.CreatePatchHome(longitude: -181).Serialize();

        // Act
        var id = await PostHome();
        await AuthorizeClientAsUser(fraud);
        await AuthorizeClientAsAdmin(client);
        var response1 = await fraud.PatchAsync($"{Paths.HomesPath}/{id}", wrongNameRequest);
        var response2 = await fraud.PatchAsync($"{Paths.HomesPath}/{id}", wrongLongitudeRequest);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response1.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, response2.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task FriendWithGetPrivilegeCanNotPatchHome()
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHome();

        await AuthorizeClientAsAdmin(friend);
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);

        var request = RequestFactory.CreatePatchHome(name: Guid.NewGuid().ToString()).Serialize();

        // Act
        var response = await friend.PatchAsync($"{Paths.HomesPath}/{home}", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task FriendWithManagePrivilegeCanPatchHome()
    {
        await PrivilegeTestBody(_managePrivilege);
    }

    private async Task PrivilegeTestBody(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHome();

        await AuthorizeClientAsAdmin(friend);
        await PrepareHomeSharingForAdmin(home, privilege);

        var request = RequestFactory.CreatePatchHome(name: Guid.NewGuid().ToString()).Serialize();

        // Act
        var response = await friend.PatchAsync($"{Paths.HomesPath}/{home}", request);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }
}