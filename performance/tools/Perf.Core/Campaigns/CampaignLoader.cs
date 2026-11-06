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
        // A knee search picks its own rates; every other campaign lists them.
        Require(campaign.Knee is not null || (NonEmptyDistinct(campaign.Rates) && campaign.Rates.All(r => r > 0)), "rates");
        Require(campaign.Knee is null || (campaign.Knee.Start > 0 && campaign.Knee.Max >= campaign.Knee.Start && campaign.Knee.Precision is > 0 and < 1), "knee");
        Require(NonEmptyDistinct(campaign.Otel), "otel");
        Require(campaign.WarmupSeconds >= 0, "warmupSeconds");
        Require(campaign.DurationSeconds > 0, "durationSeconds");
        Require(campaign.Users is { Count: > 0 } && campaign.Users.EmailPattern.Contains("{0}"), "users");
        Require(campaign.ScenarioRates is null || campaign.ScenarioRates.All(entry =>
            Enum.TryParse<Scenario>(entry.Key, out var scenario) && campaign.Scenarios.Contains(scenario)
            && NonEmptyDistinct(entry.Value) && entry.Value.All(r => r > 0)), "scenarioRates");

        return campaign with { Export = campaign.Export ?? new CampaignExport(false, false), Rates = campaign.Rates ?? [] };
    }

    private static bool NonEmptyDistinct<T>(T[]? values) => values is { Length: > 0 } && values.Distinct().Count() == values.Length;

    private static void Require(bool condition, string field)
    {
        if (!condition)
            throw new CampaignException($"Campaign field '{field}' is missing or invalid.");
    }
}
