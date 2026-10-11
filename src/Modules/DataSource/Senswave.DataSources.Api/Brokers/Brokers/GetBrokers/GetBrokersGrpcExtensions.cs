using Senswave.DataSources.Domain.Brokers.Brokers.Entities;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Brokers.GetBrokers;

internal static class GetBrokersGrpcExtensions
{
    internal static Proto.GetBrokersResponse ToProto(this IEnumerable<Broker> brokers) => new()
    {
        Items =
        {
            brokers.Select(broker => new Proto.BrokerDto
            {
                Id = broker.Id.ToString(),
                Name = broker.Name,
                Server = $"{broker.BrokerInfo.Url}:{broker.BrokerInfo.Port}",
                CreatedAt = broker.CreatedAtUtc.ToTimestamp(),
                UpdatedAt = broker.UpdatedAtUtc.ToTimestamp()
            })
        }
    };
}
