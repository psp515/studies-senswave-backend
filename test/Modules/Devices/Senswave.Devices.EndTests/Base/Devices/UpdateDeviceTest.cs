using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Senswave.Devices.Domain.Devices.Entities;
using Senswave.Devices.Domain.Devices.Enums;
using Senswave.Devices.Infrastructure;
using Senswave.TestInfrastructure.TestEnvironments.Base;
using Senswave.TestInfrastructure.TestSetup.Models;
using System.Text.Json.Nodes;

namespace Senswave.Devices.EndTests.Base.Devices;

[Trait("Collection", "EndTest")]
public class UpdateDeviceTest(BaseTestEnvironment factory) : BaseFeatureTest(factory)
{
    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ShouldBeSecuredEndpoint()
    {
        // Arrange
        var admin = CreateUnauthorizedClient();
        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var room = await PostRoom(home);
        var device = await PostDevice(home);

        var request = ToRequest(room);

        // Act
        var response = await admin.PatchAsync($"{Paths.DevicesPath}/{device}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task ValidationWorking()
    {
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var room = await PostRoom(home);
        var device = await PostDevice(home);

        var tooLongNameRequest = ToRequest(
            roomId: room,
            name: new string('a', 65),
            icon: new string('a', 65));

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{device}", tooLongNameRequest.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task MalciousDoesNotSeeDevice()
    {
        // Arrange
        var admin = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(admin);

        var userHome = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, userHome);
        var userRoom = await PostRoom(userHome);
        var userDevice = await PostDevice(userHome);

        var request = ToRequest(userRoom);

        // Act
        var response = await admin.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OwnerCanPatchDevice()
    {
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var userHome = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, userHome);
        var userRoom = await PostRoom(userHome);
        var userDevice = await PostDevice(userHome);

        var request = ToRequest(userRoom);

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task RoomShouldBeInHome()
    {
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var userHome = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, userHome);
        var anotherUserHome = await PostHome();

        var userRoom = await PostRoom(anotherUserHome);
        var userDevice = await PostDevice(userHome);

        var request = ToRequest(userRoom);

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CanAssignDeviceToRoom()
    {
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        var room = await PostRoom(home);
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var userDevice = await PostDevice(home);

        var request = ToRequest(room);

        // Act
        var patchResponse = await user.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{userDevice}");
        var content = await getResponse.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonObject>(content);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);
        Assert.NotNull(data?["roomId"]);
    }

    [Fact]
    public async Task CanUnAssignDeviceFromRoom()
    {
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        var room = await PostRoom(home);
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var userDevice = await PostDevice(home);

        var request = ToRequest(room);
        var unassignRequest = RequestFactory.CreatePatchDevice(null);

        // Act
        var patchResponse = await user.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());
        var getResponse = await user.GetAsync($"{Paths.DevicesPath}/{userDevice}");
        var assignContent = await getResponse.Content.ReadAsStringAsync();
        var assignData = JsonSerializer.Deserialize<JsonObject>(assignContent);

        var unassignResponse = await user.PatchAsync($"{Paths.DevicesPath}/{userDevice}", unassignRequest.Serialize());

        var getUnassignResponse = await user.GetAsync($"{Paths.DevicesPath}/{userDevice}");
        var unassignContent = await getUnassignResponse.Content.ReadAsStringAsync();
        var unassignData = JsonSerializer.Deserialize<JsonObject>(unassignContent);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, patchResponse.StatusCode);
        Assert.NotNull(assignData!["roomId"]);
        Assert.Null(unassignData!["roomId"]);
    }

    [Theory]
    [MemberData(nameof(ManageDevicePrivileges))]
    public async Task UserWithProperPrivilegeCanPatchDevice(string privilege)
    {
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);

        var userHome = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, userHome);
        var userRoom = await PostRoom(userHome);
        var userDevice = await PostDevice(userHome);

        var request = ToRequest(userRoom);
        await PrepareHomeSharingForAdmin(userHome);
        await PrepareDeviceSharing(privilege, userDevice);

        // Act
        var response = await friend.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Theory]
    [InlineData(_displayPrivilege)]
    [InlineData(_actionPrivilege)]
    public async Task UserWithNonProperPrivilegeCanNotPatchDevice(string privilege)
    {
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var userRoom = await PostRoom(home);
        var userDevice = await PostDevice(home);

        var request = ToRequest(userRoom);
        await PrepareHomeSharingForAdmin(home);
        await PrepareDeviceSharing(privilege, userDevice);

        // Act
        var response = await friend.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UserWithProperHomePrivilegeCanPatchDevice()
    {
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);

        var userHome = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, userHome);
        var userRoom = await PostRoom(userHome);
        var userDevice = await PostDevice(userHome);

        var request = ToRequest(userRoom);
        await PrepareHomeSharingForAdmin(userHome);

        // Act
        var response = await friend.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Trait("Category", "FailurePath")]
    [Fact]
    public async Task UserWithNonProperHomePrivilegeCanNotPatchDevice()
    {
        var friend = CreateUnauthorizedClient();
        await AuthorizeClientAsAdmin(friend);

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var userRoom = await PostRoom(home);
        var userDevice = await PostDevice(home);

        var request = ToRequest(userRoom);
        await PrepareHomeSharingForAdmin(home, _displayPrivilege);

        // Act
        var response = await friend.PatchAsync($"{Paths.DevicesPath}/{userDevice}", request.Serialize());

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CanSetDevicePresence()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var operation = await PostBooleanOperation(deviceId);

        var request = ToRequest(presenceType: "BooleanOperation", presenceOperationId: operation);

        using var scope = Factory.Server.Services.CreateScope();
        var devicesContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request.Serialize());
        var device = await devicesContext.Devices
            .Include(x => x.Presence)
            .FirstAsync(x => x.Id == deviceId);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(device.Presence);
        Assert.Equal(DevicePresenceType.BooleanOperation, device.Presence.Type);
        Assert.Equal(operation, device.Presence.OperationId);
    }

    [Fact]
    public async Task CanRevokeDevicePresence()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var operation = await PostBooleanOperation(deviceId);

        var setRequest = ToRequest(presenceType: "BooleanOperation", presenceOperationId: operation);
        var revokeRequest = ToRequest(presenceType: "Default");

        using var scope = Factory.Server.Services.CreateScope();
        var devicesContext = scope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", setRequest.Serialize());
        var revokeResponse = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", revokeRequest.Serialize());

        var device = await devicesContext.Devices
            .Include(x => x.Presence)
            .FirstAsync(x => x.Id == deviceId);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);
        Assert.NotNull(device.Presence);
        Assert.Equal(DevicePresenceType.Default, device.Presence.Type);
        Assert.Null(device.Presence.OperationId);
    }

    [Fact]
    public async Task CanUpdateDeviceWithMissingPresenceEntity()
    {
        // Arrange
        var user = CreateUnauthorizedClient();
        await AuthorizeClientAsUser(user);

        var home = await PostHome();
        var broker = await PostBroker();
        await PutBrokerForHome(broker, home);
        var deviceId = await PostDevice(home);
        var operation = await PostBooleanOperation(deviceId);

        // Simulate old system state: remove presence entity directly from DB
        using var setupScope = Factory.Server.Services.CreateScope();
        var setupContext = setupScope.ServiceProvider.GetRequiredService<DevicesContext>();
        var presenceToRemove = await setupContext.Set<DevicePresence>().FirstOrDefaultAsync(x => x.DeviceId == deviceId);
        if (presenceToRemove != null)
        {
            setupContext.Set<DevicePresence>().Remove(presenceToRemove);
            await setupContext.SaveChangesAsync();
        }

        var request = ToRequest(presenceType: "BooleanOperation", presenceOperationId: operation);

        using var assertScope = Factory.Server.Services.CreateScope();
        var devicesContext = assertScope.ServiceProvider.GetRequiredService<DevicesContext>();

        // Act
        var response = await user.PatchAsync($"{Paths.DevicesPath}/{deviceId}", request.Serialize());
        var device = await devicesContext.Devices
            .Include(x => x.Presence)
            .FirstAsync(x => x.Id == deviceId);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.NotNull(device.Presence);
        Assert.Equal(DevicePresenceType.BooleanOperation, device.Presence.Type);
        Assert.Equal(operation, device.Presence.OperationId);
    }

    private static JsonObject ToRequest(Guid? roomId = default, string name = "", string icon = "",
        string type = "", Guid operationId = default,
        string presenceType = "", Guid presenceOperationId = default) => RequestFactory.CreatePatchDevice(
        roomId: roomId,
        name: name,
        icon: icon,
        operationId: operationId,
        type: type,
        presenceOperationId: presenceOperationId,
        presenceType: presenceType);
}