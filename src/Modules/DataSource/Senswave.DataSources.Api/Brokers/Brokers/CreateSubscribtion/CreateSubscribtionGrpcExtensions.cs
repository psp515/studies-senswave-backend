using Senswave.DataSources.Application.Brokers.Brokers.Features.CreateSubscribtionForUser;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Brokers.CreateSubscribtion;

internal static class CreateSubscribtionGrpcExtensions
{
    internal static CreateSubscribtionForUserCommand ToCommand(this Proto.CreateSubscribtionRequest dto, Guid userId) => new()
    {
        UserId = userId,
        BrokerId = ProtoMapping.ParseGuid(dto.BrokerId, "brokerId"),
        Topic = dto.Topic
    };
}
