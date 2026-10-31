using System.Text.Json;

namespace Perf.Core.Campaigns;

public static class CampaignLoader
{
    public static Campaign Parse(string json)
    {
        Campaign? campaign;
        try
        {
            campaign = JsonSerializer.Deserialize<Campaign>(json, PerfJson.Options);
        }
        catch (JsonException ex)
        {
            throw new CampaignException($"Invalid campaign JSON: {ex.Message}");
        }

        if (campaign is null)
            throw new CampaignException("Campaign is empty.");

        Require(campaign.Target is not null, "target");
        Require(campaign.Rounds > 0, "rounds");
        Require(NonEmptyDistinct(campaign.Transports), "transports");
        Require(NonEmptyDistinct(campaign.Scenarios), "scenarios");
        Require(NonEmptyDistinct(campaign.Rates) && campaign.Rates.All(r => r > 0), "rates");
        Require(NonEmptyDistinct(campaign.Otel), "otel");
        Require(campaign.WarmupSeconds >= 0, "warmupSeconds");
        Require(campaign.DurationSeconds > 0, "durationSeconds");
        Require(campaign.Users is { Count: > 0 } && campaign.Users.EmailPattern.Contains("{0}"), "users");

        return campaign with { Export = campaign.Export ?? new CampaignExport(false, false) };
    }

    private static bool NonEmptyDistinct<T>(T[]? values) => values is { Length: > 0 } && values.Distinct().Count() == values.Length;

    private static void Require(bool condition, string field)
    {
        if (!condition)
            throw new CampaignException($"Campaign field '{field}' is missing or invalid.");
    }
}
