using Perf.Client;
using Perf.Core;
using Perf.Core.Campaigns;
using Perf.Core.Runs;

namespace PerfRunner;

/// <summary>Logs in every seeded user over the transport under test, (re)starts the user's broker client and collects the ids the scenarios need.</summary>
public static class Discovery
{
    public static async Task<RunData> Run(Transport transport, Campaign campaign, string brokerUser, string brokerPassword, CancellationToken ct)
    {
        await using var client = ApiClientFactory.Create(transport, new ApiTarget(campaign.Target.Api, campaign.Target.InsecureTls));
        var users = new UserData[campaign.Users.Count];

        await Parallel.ForEachAsync(Enumerable.Range(0, campaign.Users.Count), new ParallelOptions { MaxDegreeOfParallelism = 10, CancellationToken = ct },
            async (i, token) =>
            {
                var email = campaign.Users.Email(i);
                var accessToken = await client.Login(email, campaign.Users.Password, token);
                await client.StartBrokerClient(accessToken, brokerUser, brokerPassword, token);
                users[i] = await client.Discover(email, accessToken, token);
            });

        return new RunData(users);
    }
}
