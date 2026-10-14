using Senswave.Presentation.Seed.Automations.Options;
using Senswave.Presentation.Seed.DataSources.Options;
using Senswave.Presentation.Seed.Devices.Options;
using Senswave.Presentation.Seed.Homes.Options;

namespace Senswave.Presentation.Seed.Users.Options;

/// <summary>
/// Template for a batch of generated users. Every user gets the same password, role,
/// broker, home, devices and automations. <c>{0}</c> in <see cref="EmailPattern"/> and in
/// the Pico controller global topic is replaced with the user's 1-based index.
/// </summary>
public class GeneratedUsersOptions
{
    public int Count { get; set; } = 0;

    public string EmailPattern { get; set; } = "user{0}@senswave.com";
    public string Password { get; set; } = "User123456!";
    public string Role { get; set; } = "user";

    public BrokerOptions Broker { get; set; } = new();
    public HomeOptions Home { get; set; } = new();
    public DeviceOptions Devices { get; set; } = new();
    public AutomationOptions Automations { get; set; } = new();

    public IEnumerable<UserOptions> Expand()
    {
        for (int index = 1; index <= Count; index++)
        {
            yield return new UserOptions
            {
                Email = string.Format(EmailPattern, index),
                Password = Password,
                Role = Role,
                Broker = Broker,
                Home = Home,
                Automations = Automations,
                Devices = new DeviceOptions
                {
                    Detector = Devices.Detector,
                    EmptyDevice = Devices.EmptyDevice,
                    PlantMonitor = Devices.PlantMonitor,
                    PicoController = new PicoControllerOptions
                    {
                        Enabled = Devices.PicoController.Enabled,
                        GlobalTopic = string.Format(Devices.PicoController.GlobalTopic, index)
                    }
                }
            };
        }
    }
}
