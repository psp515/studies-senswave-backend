using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models.Homes;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Senswave.Homes.EndTests.Base.Homes;

[Trait("Collection", "EndTest")]
public class GetHomeTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var id = await PostHome();
        var response = await client.GetAsync($"{Paths.HomesPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserMayGetHisHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var id = await PostHome();
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanDisplay()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/{home}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var parsingResult = JsonNode.Parse(responseContent)!.AsObject();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("NotStarted", parsingResult["dataSource"]!["state"]!.ToString());
    }

    [Fact]
    public async Task OwnerCanDisplayWithoutDataSource()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();

        // Act
        await AuthorizeClientAsUser(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/{home}");
        var responseContent = await response.Content.ReadAsStringAsync();
        var parsingResult = JsonNode.Parse(responseContent)!.AsObject();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(parsingResult["dataSource"]);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserCanNotGetAnotherUserHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();

        // Act
        var id = await PostHome();
        await AuthorizeClientAsAdmin(client);
        var response = await client.GetAsync($"{Paths.HomesPath}/{id}");

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UserShouldGetListOfRoomsAvailableInHome()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var home = await PostHome();
        var room = await PostRoom(home);

        await AuthorizeClientAsUser(client);

        var response = await client.GetAsync($"{Paths.HomesPath}/{home}");
        var content = await response.Content.ReadAsStringAsync();
        var homeDto = JsonSerializer.Deserialize<GetHomeResponse>(content);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(homeDto);
        Assert.NotEmpty(homeDto.Rooms);
        Assert.NotEqual(room, homeDto.Rooms.First().Id);
    }

    [Fact]
    public async Task FriendWithGetPrivilegeCanGetHome()
    {
        await PrivilegeTestBody(_displayPrivilege);
    }


    [Fact]
    public async Task FriendWithManagePrivilegeCanGetHome()
    {
        await PrivilegeTestBody(_managePrivilege);
    }

    private async Task PrivilegeTestBody(string privilege)
    {
        // Arrange
        var friend = CreateUnauthorizedClient();
        var home = await PostHome();

        await PrepareHomeSharingForAdmin(home, privilege);
        await AuthorizeClientAsAdmin(friend);

        // Act
        var response = await friend.GetAsync($"{Paths.HomesPath}/{home}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}