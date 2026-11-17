namespace Senswave.Presentation.Seed.Devices.Options;

public class DeviceOptions
{
    public PicoControllerOptions PicoController { get; set; } = new();
    public bool Detector { get; set; }

    /// <summary>MQTT topic of the detector. Generated users replace <c>{0}</c> with their index, so every user gets its own.</summary>
    public string DetectorTopic { get; set; } = "detector/state";

    /// <summary>Seeds the lightweight device S5 and S6 use: one operation alone on its topic, one tile and one widget.</summary>
    public bool StreamDevice { get; set; }

    /// <summary>MQTT topic of the stream device. Generated users replace <c>{0}</c> with their index, so every user gets its own.</summary>
    public string StreamDeviceTopic { get; set; } = "stream/state";
    public bool EmptyDevice { get; set; }
    public bool PlantMonitor { get; set; }
}
