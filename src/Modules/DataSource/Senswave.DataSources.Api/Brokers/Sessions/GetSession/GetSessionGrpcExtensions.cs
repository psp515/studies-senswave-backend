using Senswave.DataSources.Domain.Brokers.Sessions.Entities;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Sessions.GetSession;

internal static class GetSessionGrpcExtensions
{
    internal static Proto.GetSessionResponse ToProto(this Session session) => new()
    {
        Id = session.Id.ToString(),
        CreatedAtUtc = session.CreatedAtUtc.ToTimestamp(),
        UpdatedAtUtc = session.UpdatedAtUtc.ToTimestamp(),
        Logs =
        {
            session.Logs.Select(log => new Proto.LogDto
            {
                Id = log.Id.ToString(),
                EventType = log.Type.ToString(),
                Data = log.Data,
                CreatedAtUtc = log.CreatedAtUtc.ToTimestamp()
            })
        }
    };
}
