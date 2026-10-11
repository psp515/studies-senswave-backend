using Microsoft.AspNetCore.SignalR.Client;
using Senswave.LiveUpdates.EndTests.Extensions;
using Senswave.TestInfrastructure.TestEnvironments.Base;

namespace Senswave.LiveUpdates.EndTests;

[Collection(GlobalUsings.LiveUpdatesCollection)]
[Trait("Collection", "EndTest")]
public class LiveUpdatesHubTests(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    public static List<object[]> ValidHomeSharingTypes => HomePriviliges;

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        Exception? ex = null;
        var client = CreateUnauthorizedClient();
        var connection = await client.ToSignalR(CreateHandler(), useAuthorization: false);

        // Act
        try
        {
            await connection.StartAsync();
        }
        catch (Exception e)
        {
            ex = e;
        }

        // Assert
        Assert.NotNull(ex);
        Assert.Contains("401", ex.Message);
        Assert.Equal(HubConnectionState.Disconnected, connection.State);
    }

    [Fact]
    public async Task AutorizedUserCanConnect()
    {
        // Arrange
        var client = CreateUnauthorizedClient();
        var connection = await client.ToSignalR(CreateHandler());

        // Act
        await connection.StartAsync();

        // Assert
        Assert.Equal(HubConnectionState.Connected, connection.State);
    }

    [Fact]
    public async Task UserCanInitializeUpdatesByHomeReferenceId()
    {
        // Arrange
        var initialized = false;
        var homeId = await PostHome();

        var client = CreateUnauthorizedClient();
        var connection = await client.ToSignalR(CreateHandler());
        connection.On("Initialized", () =>
        {
            initialized = true;
        });

        // Act
        await connection.StartAsync();
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await Task.Delay(GlobalUsings.DefaultTimeout);

        // Assert
        Assert.Equal(HubConnectionState.Connected, connection.State);
        Assert.True(initialized);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task IntruderCanNotInitializeByHomeReferenceId()
    {
        // Arrange
        var noAccessToHome = false;
        var homeId = await PostHome();

        var client = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(client);
        var connection = await client.ToSignalR(CreateHandler(), useAdmin: true);

        connection.On("Initialized", () =>
        {
            Assert.Fail("Initialized live update for intrueder.");
        });

        connection.On<string>("FailedToInitialize", (message) =>
        {
            noAccessToHome = true;
        });

        // Act
        await connection.StartAsync();
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await Task.Delay(GlobalUsings.DefaultTimeout);

        // Assert
        Assert.Equal(HubConnectionState.Connected, connection.State);
        Assert.True(noAccessToHome);
    }

    [Theory]
    [MemberData(nameof(ValidHomeSharingTypes))]
    public async Task FriendCanInitializeHomeLiveUpdate(string sharingType)
    {
        // Arrange
        var initialized = false;
        var homeId = await PostHome();
        await PrepareHomeSharingForAdmin(homeId, sharingType);

        var client = CreateUnauthorizedClient();
        var connection = await client.ToSignalR(CreateHandler(), useAdmin: true);
        connection.On("Initialized", () =>
        {
            initialized = true;
        });

        // Act
        await connection.StartAsync();
        await connection.InvokeAsync("Initialize", homeId.ToString());
        await Task.Delay(GlobalUsings.DefaultTimeout);

        // Assert
        Assert.Equal(HubConnectionState.Connected, connection.State);
        Assert.True(initialized);
    }
}
