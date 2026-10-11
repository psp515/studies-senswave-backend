using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Homes;
using System.Net;
using System.Text.Json;

namespace Senswave.Homes.EndTests.Base.Sharings;

[Trait("Collection", "EndTest")]
public class LeaveHomeSharingTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var user = CreateUnauthorizedClient();

        // Act 
        var response = await user.DeleteAsync($"{Paths.HomesPath}/sharings/leave/{Guid.NewGuid()}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task OwnerCanNotLeaveSharing()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var admin = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(admin);

        var home = await PostHome();
        await PrepareHomeSharingForAdmin(home);

        // Act 
        var getResponse = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponse = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponse.Content.ReadAsStringAsync());
        Assert.NotNull(jsonResponse);

        var response = await user.DeleteAsync($"{Paths.HomesPath}/sharings/leave/{home}");

        var getResponseAssert = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponseAssert = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponseAssert.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponseAssert.StatusCode);
        Assert.NotNull(jsonResponseAssert);
        Assert.NotEmpty(jsonResponseAssert.Items);
    }

    [Fact]
    public async Task FriendCanLeaveSharing()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var admin = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(admin);

        var home = await PostHome();
        await PrepareHomeSharingForAdmin(home);

        // Act 
        var getResponse = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponse = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponse.Content.ReadAsStringAsync());
        Assert.NotNull(jsonResponse);

        var response = await admin.DeleteAsync($"{Paths.HomesPath}/sharings/leave/{home}");
        var getResponseAssert = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponseAssert = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponseAssert.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponseAssert.StatusCode);
        Assert.NotNull(jsonResponseAssert);
        Assert.Empty(jsonResponseAssert.Items);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IntruderCannotLeaveHome()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var intruder = CreateUnauthorizedClient();
        await AuthorizeClientAsIntruder(intruder);

        var home = await PostHome();
        await PrepareHomeSharingForAdmin(home);

        // Act 
        var getResponse = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponse = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponse.Content.ReadAsStringAsync());
        Assert.NotNull(jsonResponse);

        var response = await intruder.DeleteAsync($"{Paths.HomesPath}/sharings/leave/{home}");

        var getResponseAssert = await user.GetAsync($"{Paths.HomesPath}/sharings?homeId={home}");
        var jsonResponseAssert = JsonSerializer.Deserialize<HomeSharingResponse>(await getResponseAssert.Content.ReadAsStringAsync());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, getResponseAssert.StatusCode);
        Assert.NotNull(jsonResponseAssert);
        Assert.NotEmpty(jsonResponseAssert.Items);
    }
}
