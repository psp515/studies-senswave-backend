using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Senswave.Abstractions.Resulting;
using Senswave.DataSources.Application.Brokers.Brokers.Features.UpdateBroker;
using Senswave.DataSources.Domain.Brokers.Brokers.Entities;
using Senswave.DataSources.Domain.Brokers.Brokers.Enums;
using Senswave.DataSources.Domain.Brokers.Brokers.Options;
using Senswave.DataSources.Domain.Brokers.Brokers.Repositories;
using Senswave.DataSources.Domain.Brokers.Brokers.Services;
using Senswave.DataSources.Domain.Brokers.Brokers.ValueObjects;
using Senswave.DataSources.Domain.Brokers.Clients.Proxy;

namespace Senswave.DataSources.UnitTests.DataSources.Features.UpdateBroker;

[Trait("Collection", "UnitTests")]
public class SharedBrokerTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid BrokerId = Guid.NewGuid();

    private static readonly UpdateBrokerCommand Command = new()
    {
        UserId = UserId,
        BrokerId = BrokerId,
        Url = "shared.example.com",
        Port = 8883
    };

    private static (UpdateBrokerHandler Handler, Mock<IBrokerQueryRepository> Query) Build(bool allowSharedBrokers)
    {
        var broker = new Broker
        {
            Id = BrokerId,
            OwnerId = UserId,
            Name = "Broker",
            BrokerInfo = new BrokerInfo
            {
                Url = "old.example.com",
                Port = 1883,
                ClientName = "client-1",
                ProtocolVersion = BrokerProtocolVersion.MqttV5,
                UseTls = true
            }
        };

        var query = new Mock<IBrokerQueryRepository>();
        query.Setup(q => q.BrokerExists(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var command = new Mock<IBrokerCommandRepository>();
        command.Setup(c => c.GetBroker(BrokerId, UserId, It.IsAny<CancellationToken>())).ReturnsAsync(broker);
        command.Setup(c => c.UpdateBroker(It.IsAny<Broker>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var proxy = new Mock<IClientProxy>();
        proxy.Setup(p => p.ClientExists(BrokerId, It.IsAny<CancellationToken>())).ReturnsAsync(Result.Failure());

        var options = Options.Create(new BrokerOptions
        {
            TestConnectionOnChange = false,
            AllowSharedBrokers = allowSharedBrokers
        });

        var handler = new UpdateBrokerHandler(
            Mock.Of<IBrokerService>(),
            proxy.Object,
            query.Object,
            command.Object,
            options,
            NullLogger<UpdateBrokerHandler>.Instance);

        return (handler, query);
    }

    [Fact]
    public async Task SharedBrokersDisallowed_ExistingUrlAndPort_Rejected()
    {
        var (handler, _) = Build(allowSharedBrokers: false);

        var result = await handler.Handle(Command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == UpdateBrokerErrors.BrokerAlreadyUsed.Code);
    }

    [Fact]
    public async Task SharedBrokersAllowed_ExistingUrlAndPort_Updated()
    {
        var (handler, query) = Build(allowSharedBrokers: true);

        var result = await handler.Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        query.Verify(q => q.BrokerExists(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
