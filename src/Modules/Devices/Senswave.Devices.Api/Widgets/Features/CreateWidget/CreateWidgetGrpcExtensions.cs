using Senswave.Devices.Application.Widgets.Features.CreateWidget;
using Senswave.Devices.Domain.Widgets.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Widgets.Features.CreateWidget;

internal static class CreateWidgetGrpcExtensions
{
    internal static CreateWidgetCommand ToCommand(this Proto.CreateWidgetRequest dto, Guid userId) => new()
    {
        UserId = userId,
        OperationId = ProtoMapping.ParseGuid(dto.OperationId, "operationId"),
        Name = dto.Name,
        Configuration = dto.Configuration.ToJsonObject("configuration"),
        Type = dto.Type.ToWidgetType()
    };
}
