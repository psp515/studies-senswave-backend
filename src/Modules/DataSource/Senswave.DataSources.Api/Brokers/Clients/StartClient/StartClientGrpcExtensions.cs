using Senswave.DataSources.Application.Brokers.Clients.StartClient;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Clients.StartClient;

internal static class StartClientGrpcExtensions
{
    internal static StartClientCommand ToCommand(this Proto.StartClientRequest dto, Guid userId) => new()
    {
        BrokerId = ProtoMapping.ParseGuid(dto.BrokerId, "brokerId"),
        UserId = userId,
        Username = dto.Username,
        Password = dto.Password
    };
}
