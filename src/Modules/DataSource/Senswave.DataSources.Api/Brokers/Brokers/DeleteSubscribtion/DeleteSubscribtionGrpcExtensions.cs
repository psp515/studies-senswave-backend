using Senswave.DataSources.Application.Brokers.Brokers.Features.DeleteSubscribtion;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.DataSources.V1;

namespace Senswave.DataSources.Api.Brokers.Brokers.DeleteSubscribtion;

internal static class DeleteSubscribtionGrpcExtensions
{
    internal static DeleteSubscribtionCommand ToCommand(this Proto.DeleteSubscribtionRequest dto, Guid userId) => new()
    {
        SubscriptionId = ProtoMapping.ParseGuid(dto.SubscriptionId, "subscriptionId"),
        BrokerId = ProtoMapping.ParseGuid(dto.BrokerId, "brokerId"),
        UserId = userId
    };
}
