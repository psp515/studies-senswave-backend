using Senswave.DataSources.Domain.Brokers.Sessions.Entities;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Sessions.GetSessions;

internal static class GetSessionsGrpcExtensions
{
    internal static Proto.GetSessionsResponse ToProto(this IEnumerable<Session> sessions) => new()
    {
        Items =
        {
            sessions.Select(session => new Proto.SessionDto
            {
                Id = session.Id.ToString(),
                CreatedAtUtc = session.CreatedAtUtc.ToTimestamp(),
                UpdatedAtUtc = session.UpdatedAtUtc.ToTimestamp(),
                Finished = session.Finished,
            })
        }
    };
}
