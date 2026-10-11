using Senswave.Devices.Domain.Widgets.Entities;
using Senswave.Devices.Domain.Widgets.Extensions;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Widgets.Features.GetWidget;

internal static class GetWidgetGrpcExtensions
{
    internal static Proto.GetWidgetResponse ToProto(this Widget widget) => new()
    {
        Id = widget.Id.ToString(),
        DeviceId = widget.Operation!.DeviceId.ToString(),
        OperationId = widget.OperationId.ToString(),
        Name = widget.Name,
        Type = widget.Type.FromWidgetType(),
        Configuration = widget.Configuration.ToStruct(),
        Enabled = widget.Enabled,
    };
}
