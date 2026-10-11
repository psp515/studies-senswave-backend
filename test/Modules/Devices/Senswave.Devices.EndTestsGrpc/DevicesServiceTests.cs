using Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class DevicesServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateDevice → GetDevice → DisplayDevice → DisplayDevices → UpdateDevice (switch tile) → GetDevice → DeleteDevice.
    /// DeviceTileAction needs a live broker client and is covered by <see cref="DeviceActionsMqttTests"/>.
    /// </summary>
    [Fact]
    public async Task DeviceLifecycle()
    {
        // Arrange
        var channel = await CreateAuthorizedChannelAsUser();
        var client = new DevicesService.DevicesServiceClient(channel);
        var homeId = await channel.CreateHomeWithBrokerAsync();
        var roomId = await channel.CreateRoomAsync(homeId);

        // CreateDevice
        var created = await client.CreateDeviceAsync(new CreateDeviceRequest { HomeId = homeId, RoomId = roomId, Name = "Lamp", Icon = "lamp" });
        var deviceId = created.Id;

        // GetDevice + DisplayDevice + DisplayDevices
        var device = await client.GetDeviceAsync(new GetDeviceRequest { DeviceId = deviceId });
        var display = await client.DisplayDeviceAsync(new DisplayDeviceRequest { DeviceId = deviceId });
        var devices = await client.DisplayDevicesAsync(new DisplayDevicesRequest { HomeId = homeId });

        Assert.Equal("Lamp", device.Name);
        Assert.Equal(roomId, device.RoomId);
        Assert.Equal(deviceId, display.Device.Id);
        Assert.Contains(devices.Items, x => x.Id == deviceId);

        // UpdateDevice: rename and assign a switch tile
        var operationId = await channel.CreateBooleanOperationAsync(deviceId);
        await client.UpdateDeviceAsync(new UpdateDeviceRequest
        {
            DeviceId = deviceId,
            RoomId = roomId,
            Name = "Desk lamp",
            Icon = "lamp",
            OperationId = operationId,
            Type = "Switch",
        });
        var updated = await client.GetDeviceAsync(new GetDeviceRequest { DeviceId = deviceId });

        Assert.Equal("Desk lamp", updated.Name);
        Assert.Equal("Switch", updated.Tile.Type);
        Assert.Equal(operationId, updated.Tile.OperationId);

        // DeleteDevice (a device with operations cannot be deleted, so an empty one is used)
        var spareId = await channel.CreateDeviceAsync(homeId);
        await client.DeleteDeviceAsync(new DeleteDeviceRequest { DeviceId = spareId });
    }
}
