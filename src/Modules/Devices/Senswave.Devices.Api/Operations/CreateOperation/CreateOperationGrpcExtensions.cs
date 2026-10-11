using Senswave.Devices.Application.Operations.Features.CreateOperation;
using Senswave.Devices.Domain.Operations.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Operations.CreateOperation;

internal static class CreateOperationGrpcExtensions
{
    internal static CreateOperationCommand ToCommand(this Proto.CreateOperationRequest dto, Guid userId) => new()
    {
        DeviceId = ProtoMapping.ParseGuid(dto.DeviceId, "deviceId"),
        Name = dto.Name,
        Configuration = dto.Configuration.ToJsonObject("configuration"),
        UserId = userId,
        Topic = dto.Topic,
        Type = dto.Type.ToOperationType()
    };
}
