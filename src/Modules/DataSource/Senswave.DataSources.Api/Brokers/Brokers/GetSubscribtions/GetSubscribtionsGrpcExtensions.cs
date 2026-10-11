using Senswave.DataSources.Domain.Brokers.Brokers.Entities;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Brokers.GetSubscribtions;

internal static class GetSubscribtionsGrpcExtensions
{
    internal static Proto.GetSubscribtionsResponse ToProto(this IEnumerable<Subscribtion> subscribtions) => new()
    {
        Items =
        {
            subscribtions.Select(subscribtion => new Proto.SubscribtionDto
            {
                Id = subscribtion.Id.ToString(),
                Topic = subscribtion.Topic,
                CreatedAt = subscribtion.CreatedAtUtc.ToTimestamp(),
                UpdatedAt = subscribtion.UpdatedAtUtc.ToTimestamp()
            })
        }
    };
}
