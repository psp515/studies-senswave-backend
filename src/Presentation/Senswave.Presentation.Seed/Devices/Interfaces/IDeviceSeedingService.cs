using Senswave.Abstractions.Resulting;
using Senswave.Presentation.Seed.Devices.Options;
using Senswave.Presentation.Seed.Devices.Types;

namespace Senswave.Presentation.Seed.Devices.Interfaces;

public interface IDeviceSeedingService
{
    Task<Result<DeviceOperations>> SeedPicoController(string accessToken, Guid homeId, Guid? roomId, PicoControllerOptions options);

    Task<Result<DeviceOperations>> SeedDetector(string accessToken, Guid homeId, string topic);

    Task<Result> SeedStreamDevice(string accessToken, Guid homeId, string topic);

    Task<Result> SeedEmptyDevice(string accessToken, Guid homeId);

    Task<Result> SeedPlantMonitor(string accessToken, Guid homeId);
}
