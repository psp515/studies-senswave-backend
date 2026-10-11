using Senswave.DataSources.Domain.Brokers.Brokers.Entities;
using Senswave.DataSources.Domain.Brokers.Brokers.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Brokers.GetBroker;

internal static class GetBrokerGrpcExtensions
{
    internal static Proto.GetBrokerResponse ToProto(this Broker broker) => new()
    {
        Id = broker.Id.ToString(),
        Name = broker.Name,
        Url = broker.BrokerInfo.Url,
        ClientName = broker.BrokerInfo.ClientName,
        Port = broker.BrokerInfo.Port,
        ProtocolVersion = broker.BrokerInfo.ProtocolVersion.FromProtocol(),
        UseTls = broker.BrokerInfo.UseTls,
        CreatedAt = broker.CreatedAtUtc.ToTimestamp(),
        UpdatedAt = broker.UpdatedAtUtc.ToTimestamp()
    };
}
