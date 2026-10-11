using Senswave.DataSources.Application.Brokers.Brokers.Features.CreateBroker;
using Senswave.DataSources.Domain.Brokers.Brokers.Extensions;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Brokers.CreateBroker;

internal static class CreateBrokerGrpcExtensions
{
    internal static CreateBrokerCommand ToCommand(this Proto.CreateBrokerRequest dto, Guid userId) => new()
    {
        UserId = userId,
        Name = dto.Name,
        Url = dto.Url,
        ClientName = dto.ClientName,
        Port = dto.Port,
        ProtocolVersion = dto.ProtocolVersion.ToProtocol(),
        UseTls = dto.UseTls,
        Password = dto.Password,
        Username = dto.Username
    };
}
