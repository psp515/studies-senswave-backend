namespace Senswave.Presentation.Seed.Devices.Options;

public class DeviceOptions
{
    public PicoControllerOptions PicoController { get; set; } = new();
    public bool Detector { get; set; }

    /// <summary>MQTT topic of the detector. Generated users replace <c>{0}</c> with their index, so every user gets its own.</summary>
    public string DetectorTopic { get; set; } = "detector/state";
    public bool EmptyDevice { get; set; }
    public bool PlantMonitor { get; set; }
}
