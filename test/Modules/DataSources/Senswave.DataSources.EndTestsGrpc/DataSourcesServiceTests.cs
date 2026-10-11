using Microsoft.Extensions.DependencyInjection;
using Senswave.Contracts.DataSources.V1;
using Senswave.DataSources.Domain.Brokers.Sessions.Entities;
using Senswave.DataSources.Domain.Brokers.Sessions.Enums;
using Senswave.DataSources.Infrastructure;

namespace Senswave.DataSources.EndTestsGrpc;

[Trait("Collection", "EndTestGrpc")]
public class DataSourcesServiceTests(GrpcTestEnvironment environment) : GrpcFeatureTest(environment)
{
    /// <summary>
    /// CreateBroker → GetBroker → GetBrokers → UpdateBroker → CreateSubscribtion → GetSubscribtions → DeleteSubscribtion
    /// → GetClientState → GetSessions → GetSession → DeleteBroker.
    /// </summary>
    [Fact]
    public async Task BrokerLifecycle()
    {
        // Arrange
        var client = await CreateAuthorizedClientAsUser<DataSourcesService.DataSourcesServiceClient>();
        var name = Guid.NewGuid().ToString()[..20];

        // CreateBroker
        var created = await client.CreateBrokerAsync(new CreateBrokerRequest
        {
            Name = name,
            Url = $"{Guid.NewGuid():N}.example.com",
            ClientName = Guid.NewGuid().ToString(),
            Port = Random.Shared.Next(5000, 10000),
            ProtocolVersion = "MqttV5",
        });
        var brokerId = created.Id;

        // GetBroker + GetBrokers
        var broker = await client.GetBrokerAsync(new GetBrokerRequest { BrokerId = brokerId });
        var brokers = await client.GetBrokersAsync(new GetBrokersRequest { Page = 1, Size = 100 });

        Assert.Equal(name, broker.Name);
        Assert.Equal("MqttV5", broker.ProtocolVersion);
        Assert.Contains(brokers.Items, x => x.Id == brokerId);

        // UpdateBroker
        await client.UpdateBrokerAsync(new UpdateBrokerRequest { BrokerId = brokerId, Name = "Renamed broker" });
        var updated = await client.GetBrokerAsync(new GetBrokerRequest { BrokerId = brokerId });

        Assert.Equal("Renamed broker", updated.Name);

        // CreateSubscribtion + GetSubscribtions + DeleteSubscribtion
        var subscription = await client.CreateSubscribtionAsync(new CreateSubscribtionRequest { BrokerId = brokerId, Topic = "senswave/test" });
        var subscriptions = await client.GetSubscribtionsAsync(new GetSubscribtionsRequest { BrokerId = brokerId });

        Assert.Contains(subscriptions.Items, x => x.Id == subscription.Id && x.Topic == "senswave/test");

        await client.DeleteSubscribtionAsync(new DeleteSubscribtionRequest { BrokerId = brokerId, SubscriptionId = subscription.Id });

        // GetClientState
        var state = await client.GetClientStateAsync(new GetClientStateRequest { BrokerId = brokerId });

        Assert.Equal("NotStarted", state.ConnectionStatus);

        // GetSessions + GetSession (sessions are created by the broker worker, so one is seeded directly)
        var sessionId = await SeedSession(Guid.Parse(brokerId));
        var sessions = await client.GetSessionsAsync(new GetSessionsRequest { BrokerId = brokerId });
        var session = await client.GetSessionAsync(new GetSessionRequest { BrokerId = brokerId, SessionId = sessionId.ToString() });

        Assert.Contains(sessions.Items, x => x.Id == sessionId.ToString());
        Assert.Equal(sessionId.ToString(), session.Id);
        Assert.Single(session.Logs);

        // DeleteBroker
        await client.DeleteBrokerAsync(new DeleteBrokerRequest { BrokerId = brokerId });
    }

    private async Task<Guid> SeedSession(Guid brokerId)
    {
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<DataSourcesContext>();

        var broker = await context.Brokers.FindAsync(brokerId);

        var session = new Session
        {
            Broker = broker!,
            Logs = [],
            Finished = false,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        };

        session.Logs.Add(new Log
        {
            Session = session,
            Type = SessionEventType.Connected,
            Data = string.Empty,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });

        context.Sessions.Add(session);
        await context.SaveChangesAsync();

        return session.Id;
    }
}
