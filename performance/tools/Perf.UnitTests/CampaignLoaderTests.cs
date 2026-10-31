using Perf.Core;
using Perf.Core.Campaigns;

namespace Perf.UnitTests;

public class CampaignLoaderTests
{
    private const string Valid = """
    {
      "target": { "api": "https://localhost:8443", "agent": "https://localhost:9443", "insecureTls": true },
      "rounds": 3,
      "transports": ["Rest", "Grpc", "GrpcWeb"],
      "scenarios": ["S1", "S6a"],
      "rates": [100, 500, 1000],
      "otel": [true, false],
      "warmupSeconds": 30,
      "durationSeconds": 300,
      "users": { "emailPattern": "user{0}@gmail.com", "firstIndex": 1, "count": 100, "password": "User123456!" },
      "export": { "traces": true, "rawK6": false }
    }
    """;

    [Fact]
    public void ParsesValidCampaign()
    {
        var campaign = CampaignLoader.Parse(Valid);

        Assert.Equal(3, campaign.Rounds);
        Assert.Equal([Transport.Rest, Transport.Grpc, Transport.GrpcWeb], campaign.Transports);
        Assert.Equal([Scenario.S1, Scenario.S6a], campaign.Scenarios);
        Assert.Equal([100, 500, 1000], campaign.Rates);
        Assert.True(campaign.Target.InsecureTls);
        Assert.Equal(100, campaign.Users.Count);
    }

    [Theory]
    [InlineData("\"rounds\": 3", "\"rounds\": 0", "rounds")]
    [InlineData("\"rates\": [100, 500, 1000]", "\"rates\": []", "rates")]
    [InlineData("\"durationSeconds\": 300", "\"durationSeconds\": 0", "durationSeconds")]
    [InlineData("\"transports\": [\"Rest\", \"Grpc\", \"GrpcWeb\"]", "\"transports\": [\"Rest\", \"Rest\"]", "transports")]
    public void RejectsInvalidField(string from, string to, string field)
    {
        var ex = Assert.Throws<CampaignException>(() => CampaignLoader.Parse(Valid.Replace(from, to)));

        Assert.Contains(field, ex.Message);
    }
}
