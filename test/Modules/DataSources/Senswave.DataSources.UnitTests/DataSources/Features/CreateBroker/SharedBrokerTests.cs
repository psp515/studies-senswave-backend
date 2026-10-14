using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Senswave.Abstractions.Resulting;
using Senswave.DataSources.Application.Brokers.Brokers.Features.CreateBroker;
using Senswave.DataSources.Domain.Brokers.Brokers.Entities;
using Senswave.DataSources.Domain.Brokers.Brokers.Enums;
using Senswave.DataSources.Domain.Brokers.Brokers.Options;
using Senswave.DataSources.Domain.Brokers.Brokers.Repositories;
using Senswave.DataSources.Domain.Brokers.Brokers.Services;

namespace Senswave.DataSources.UnitTests.DataSources.Features.CreateBroker;

[Trait("Collection", "UnitTests")]
public class SharedBrokerTests
{
    private static readonly CreateBrokerCommand Command = new()
    {
        UserId = Guid.NewGuid(),
        Name = "Broker",
        Url = "broker.example.com",
        Port = 8883,
        ClientName = "client-1",
        ProtocolVersion = BrokerProtocolVersion.MqttV5,
        UseTls = true
    };

    private static (PostBrokerConnectionHandler Handler, Mock<IBrokerQueryRepository> Query) Build(bool allowSharedBrokers)
    {
        var query = new Mock<IBrokerQueryRepository>();
        query.Setup(q => q.BrokerNameUsedByUser(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        query.Setup(q => q.BrokerExists(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        query.Setup(q => q.ClientNameIsUsedForBroker(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        query.Setup(q => q.CountBrokersForUser(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(0);
        query.Setup(q => q.CountGlobalBrokers(It.IsAny<CancellationToken>())).ReturnsAsync(0);

        var command = new Mock<IBrokerCommandRepository>();
        command.Setup(c => c.CreateBroker(It.IsAny<Broker>(), It.IsAny<CancellationToken>())).ReturnsAsync(Result.Success());

        var options = Options.Create(new BrokerOptions
        {
            TestConnectionOnChange = false,
            AllowSharedBrokers = allowSharedBrokers
        });

        var handler = new PostBrokerConnectionHandler(
            Mock.Of<IBrokerService>(),
            query.Object,
            command.Object,
            options,
            NullLogger<PostBrokerConnectionHandler>.Instance);

        return (handler, query);
    }

    [Fact]
    public async Task SharedBrokersDisallowed_ExistingUrlAndPort_Rejected()
    {
        var (handler, _) = Build(allowSharedBrokers: false);

        var result = await handler.Handle(Command, CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, e => e.Code == CreateBrokerErrors.BrokerAlreadyUsed.Code);
    }

    [Fact]
    public async Task SharedBrokersAllowed_ExistingUrlAndPort_Created()
    {
        var (handler, query) = Build(allowSharedBrokers: true);

        var result = await handler.Handle(Command, CancellationToken.None);

        Assert.True(result.IsSuccess);
        query.Verify(q => q.BrokerExists(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
