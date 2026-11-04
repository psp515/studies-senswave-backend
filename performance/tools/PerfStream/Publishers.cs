using MQTTnet;
using MQTTnet.Client;
using Perf.Core.Runs;

namespace PerfStream;

public interface IPublisher : IAsyncDisposable
{
    /// <summary>Triggers one live update for <paramref name="user"/>'s device.</summary>
    Task Publish(UserData user, long sequence, CancellationToken ct);
}

/// <summary>S6: acts as the device and publishes to the MQTT broker; the API receives the message and pushes the update.</summary>
public sealed class MqttPublisher(IMqttClient client) : IPublisher
{
    public static async Task<MqttPublisher> Connect(StreamSettings s, CancellationToken ct)
    {
        var client = new MqttFactory().CreateMqttClient();
        var options = new MqttClientOptionsBuilder().WithTcpServer(s.MqttHost, s.MqttPort).WithClientId($"perfstream-{Guid.NewGuid():N}");
        if (s.MqttUser is { Length: > 0 })
            options = options.WithCredentials(s.MqttUser, s.MqttPassword);
        if (s.MqttTls)
            options = options.WithTlsOptions(o =>
            {
                o.UseTls();
                if (s.Insecure)
                    o.WithCertificateValidationHandler(_ => true);
            });
        await client.ConnectAsync(options.Build(), ct);
        return new MqttPublisher(client);
    }

    public async Task Publish(UserData user, long sequence, CancellationToken ct)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(DeviceTopic(user))
            .WithPayload(DevicePayload(sequence))
            .Build();
        await client.PublishAsync(message, ct);
    }

    // The seeded Pico controller's tile is a Switch bound to the "Controller State" operation:
    // topic = Seed GlobalTopic "perf/controller/{n}" (n from user{n}@gmail.com), JSON key "working".
    internal static string DeviceTopic(UserData user) =>
        $"perf/controller/{System.Text.RegularExpressions.Regex.Match(user.Email, @"\d+").Value}";

    // Alternate so every message is a value change (sequence is a per-user counter).
    internal static string DevicePayload(long sequence) => sequence % 2 == 0 ? "{\"working\":true}" : "{\"working\":false}";

    public async ValueTask DisposeAsync()
    {
        try { await client.DisconnectAsync(); } catch { /* best effort */ }
        client.Dispose();
    }
}
