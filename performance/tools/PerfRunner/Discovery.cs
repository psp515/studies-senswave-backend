using Perf.Client;
using Perf.Core;
using Perf.Core.Campaigns;
using Perf.Core.Runs;

namespace PerfRunner;

/// <summary>
/// Logs in every seeded user over the transport under test and collects the ids the scenarios need. The user's broker client
/// (one MQTT connection per user, held by the API) is started only for the scenarios that publish (S5, S6), so S1-S4
/// do not run next to thousands of idle MQTT connections.
/// </summary>
public static class Discovery
{
    public static async Task<RunData> Run(Transport transport, Scenario scenario, Campaign campaign, string brokerUser, string brokerPassword, CancellationToken ct)
    {
        await using var client = ApiClientFactory.Create(transport, new ApiTarget(campaign.Target.Api, campaign.Target.InsecureTls));
        var users = new UserData[campaign.Users.Count];

        await Parallel.ForEachAsync(Enumerable.Range(0, campaign.Users.Count), new ParallelOptions { MaxDegreeOfParallelism = 20, CancellationToken = ct },
            async (i, token) =>
            {
                var email = campaign.Users.Email(i);
                var accessToken = await client.Login(email, campaign.Users.Password, token);
                if (scenario is Scenario.S5 or Scenario.S6)
                    await client.StartBrokerClient(accessToken, brokerUser, brokerPassword, token);
                users[i] = await client.Discover(email, accessToken, token);
            });

        return new RunData(users);
    }
}
