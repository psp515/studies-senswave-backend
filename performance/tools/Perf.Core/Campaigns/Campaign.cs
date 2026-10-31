namespace Perf.Core.Campaigns;

public sealed record CampaignTarget(Uri Api, Uri Agent, bool InsecureTls);

public sealed record CampaignUsers(string EmailPattern, int FirstIndex, int Count, string Password)
{
    public string Email(int i) => string.Format(EmailPattern, FirstIndex + i);
}

public sealed record CampaignExport(bool Traces, bool RawK6);

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
    CampaignExport Export);

public sealed class CampaignException(string message) : Exception(message);
