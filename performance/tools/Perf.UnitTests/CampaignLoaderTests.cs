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
      "scenarios": ["S1", "S6"],
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
        Assert.Equal([Scenario.S1, Scenario.S6], campaign.Scenarios);
        Assert.Equal([100, 500, 1000], campaign.Rates);
        Assert.True(campaign.Target.InsecureTls);
        Assert.Equal(100, campaign.Users.Count);
    }

    [Fact]
    public void ParsesScenarioRatesAndKneeMode()
    {
        var campaign = CampaignLoader.Parse(Valid.Replace("\"rounds\": 3,", "\"rounds\": 3, \"scenarioRates\": { \"S6\": [20, 60] }, \"stopAfterSaturated\": true,"));

        Assert.True(campaign.StopAfterSaturated);
        Assert.Equal([20, 60], campaign.RatesFor(Scenario.S6));
        Assert.Equal([100, 500, 1000], campaign.RatesFor(Scenario.S1));
    }

    [Fact]
    public void KneeCampaignNeedsNoRates()
    {
        var campaign = CampaignLoader.Parse(Valid.Replace("\"rates\": [100, 500, 1000],", "\"knee\": { \"max\": 2000, \"precision\": 50 },"));

        Assert.Equal(new CampaignKnee(2000, 50), campaign.Knee);
        Assert.Empty(campaign.Rates);
    }

    [Theory]
    [InlineData("{ \"max\": 2000, \"precision\": 0 }")]
    [InlineData("{ \"max\": 50, \"precision\": 50 }")]
    public void RejectsInvalidKnee(string knee)
    {
        var ex = Assert.Throws<CampaignException>(() => CampaignLoader.Parse(Valid.Replace("\"rates\": [100, 500, 1000],", $"\"knee\": {knee},")));

        Assert.Contains("knee", ex.Message);
    }

    [Theory]
    [InlineData("{ \"S9\": [10] }")]          // unknown scenario
    [InlineData("{ \"S4\": [10] }")]          // scenario not in the campaign
    [InlineData("{ \"S1\": [] }")]            // empty
    [InlineData("{ \"S1\": [10, 10] }")]      // duplicates
    public void RejectsInvalidScenarioRates(string scenarioRates)
    {
        var ex = Assert.Throws<CampaignException>(() => CampaignLoader.Parse(Valid.Replace("\"rounds\": 3,", $"\"rounds\": 3, \"scenarioRates\": {scenarioRates},")));

        Assert.Contains("scenarioRates", ex.Message);
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
