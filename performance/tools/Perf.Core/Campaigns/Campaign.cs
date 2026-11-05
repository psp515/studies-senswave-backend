namespace Perf.Core.Campaigns;

public sealed record CampaignTarget(Uri Api, Uri Agent, bool InsecureTls);

public sealed record CampaignUsers(string EmailPattern, int FirstIndex, int Count, string Password)
{
    public string Email(int i) => string.Format(EmailPattern, FirstIndex + i);
}

public sealed record CampaignExport(bool Traces, bool RawK6);

/// <summary>Knee search by bisection: start at <paramref name="Max"/> requests or events per second, stop at <paramref name="Precision"/>.</summary>
public sealed record CampaignKnee(int Max, int Precision);

public sealed record Campaign(
    CampaignTarget Target,
    int Rounds,
    Transport[] Transports,
    Scenario[] Scenarios,
    int[] Rates,
    bool[] Otel,
    int WarmupSeconds,
    int DurationSeconds,
    CampaignUsers Users,
    CampaignExport Export,
    IReadOnlyDictionary<string, int[]>? ScenarioRates = null,
    bool StopAfterSaturated = false,
    CampaignKnee? Knee = null)
{
    /// <summary>The rates of one scenario: its entry in <see cref="ScenarioRates"/>, otherwise <see cref="Rates"/>.</summary>
    public int[] RatesFor(Scenario scenario) =>
        ScenarioRates is not null && ScenarioRates.TryGetValue(scenario.ToString(), out var rates) ? rates : Rates;
}

public sealed class CampaignException(string message) : Exception(message);
