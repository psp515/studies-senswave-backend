using Senswave.Devices.Application.Widgets.Features.DisplayWidgets;
using Senswave.Devices.Domain.Operations.Extensions;
using Senswave.Devices.Domain.Widgets.Extensions;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Widgets.Features.DisplayWidgets;

internal static class DisplayWidgetsGrpcExtensions
{
    internal static Proto.DisplayWidgetsResponse ToProto(this IEnumerable<DisplayWidgetsGroupModel> groups) => new()
    {
        Items =
        {
            groups.Select(model => new Proto.DisplayGroupDto
            {
                Operation = new Proto.WidgetOperationDto
                {
                    Id = model.Operation.Id.ToString(),
                    Name = model.Operation.Name,
                    Type = model.Operation.Type.FromOperationType()
                },
                Widgets =
                {
                    model.Widgets.Select(widget => new Proto.WidgetDto
                    {
                        Id = widget.Id.ToString(),
                        Name = widget.Name,
                        Enabled = widget.Enabled,
                        Type = widget.Type.FromWidgetType()
                    })
                }
            })
        }
    };
}
