using Senswave.DataSources.Application.Brokers.Brokers.Features.UpdateBroker;
using Senswave.DataSources.Domain.Brokers.Brokers.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Brokers.UpdateBroker;

internal static class UpdateBrokerGrpcExtensions
{
    internal static UpdateBrokerCommand ToCommand(this Proto.UpdateBrokerRequest dto, Guid userId)
    {
        var command = new UpdateBrokerCommand()
        {
            UserId = userId,
            BrokerId = ProtoMapping.ParseGuid(dto.BrokerId, "brokerId"),
            Name = dto.Name,
            Url = dto.Url,
            ClientName = dto.ClientName,
            ProtocolVersion = dto.ProtocolVersion.ToProtocol(),
            Password = dto.Password,
            Username = dto.Username
        };

        if (dto.HasPort && dto.Port != 0)
            command.Port = dto.Port;

        if (dto.HasUseTls)
            command.UseTls = dto.UseTls;

        return command;
    }
}
