using Perf.Core.Runs;
using PerfStream;

namespace Perf.UnitTests;

public class PublishersTests
{
    private static UserData User(string email) => new(email, "token", "home", "pico", "dashboard", "stream");

    [Theory]
    [InlineData("user1@gmail.com", "perf/stream/1")]
    [InlineData("user2000@gmail.com", "perf/stream/2000")]
    public void DeviceTopicIsTheUsersStreamTopic(string email, string topic) =>
        Assert.Equal(topic, MqttPublisher.DeviceTopic(User(email)));

    [Fact]
    public void DevicePayloadAlternatesSoEveryMessageIsAValueChange()
    {
        Assert.Equal("{\"state\":true}", MqttPublisher.DevicePayload(0));
        Assert.Equal("{\"state\":false}", MqttPublisher.DevicePayload(1));
        Assert.Equal("{\"state\":true}", MqttPublisher.DevicePayload(2));
    }
}
