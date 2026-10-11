using Grpc.Core;
using Senswave.Contracts.Common.V1;
using Senswave.Devices.Api.Devices.CreateDevice;
using Senswave.Devices.Api.Devices.DeleteDevice;
using Senswave.Devices.Api.Devices.DisplayDevice;
using Senswave.Devices.Api.Devices.GetDevice;
using Senswave.Devices.Api.Devices.UpdateDevice;
using Senswave.Devices.Application.Devices.Features.DeviceTileAction;
using Senswave.Devices.Application.Devices.Features.DisplayDevice;
using Senswave.Devices.Application.Devices.Features.DisplayDevices;
using Senswave.Devices.Application.Devices.Features.GetDevice;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Devices.V1;

namespace Senswave.Devices.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class DevicesGrpcService(IMediator mediator, IRequestContext requestContext) : Proto.DevicesService.DevicesServiceBase
{
    public override async Task<IdResponse> CreateDevice(Proto.CreateDeviceRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.UpdateDeviceResponse> UpdateDevice(Proto.UpdateDeviceRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.UpdateDeviceResponse();
    }

    public override async Task<Proto.DeleteDeviceResponse> DeleteDevice(Proto.DeleteDeviceRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.DeviceId, "deviceId").ToDeleteDeviceCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteDeviceResponse();
    }

    public override async Task<Proto.GetDeviceResponse> GetDevice(Proto.GetDeviceRequest request, ServerCallContext context)
    {
        var query = new GetDeviceQuery
        {
            DeviceId = ProtoMapping.ParseGuid(request.DeviceId, "deviceId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return result.Data.ToProto();
    }

    public override async Task<Proto.DisplayDeviceResponse> DisplayDevice(Proto.DisplayDeviceRequest request, ServerCallContext context)
    {
        var query = new DisplayDeviceQuery
        {
            UserId = requestContext.UserId,
            DeviceId = ProtoMapping.ParseGuid(request.DeviceId, "deviceId")
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DisplayDeviceResponse { Device = result.Data.ToProto() };
    }

    public override async Task<Proto.DisplayDevicesResponse> DisplayDevices(Proto.DisplayDevicesRequest request, ServerCallContext context)
    {
        var query = new DisplayDevicesQuery
        {
            UserId = requestContext.UserId,
            HomeReferenceId = ProtoMapping.ParseGuid(request.HomeId, "homeId"),
            Page = request.HasPage ? request.Page : 1,
            Size = request.HasSize ? request.Size : 10
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DisplayDevicesResponse { Items = { result.Data.Select(x => x.ToProto()) } };
    }

    public override async Task<Proto.DeviceTileActionResponse> DeviceTileAction(Proto.DeviceTileActionRequest request, ServerCallContext context)
    {
        var command = new DeviceTileActionCommand
        {
            DeviceId = ProtoMapping.ParseGuid(request.DeviceId, "deviceId"),
            UserId = requestContext.UserId,
            Value = request.Value.ToJsonValue("value")
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeviceTileActionResponse { Device = result.Data.ToProto() };
    }
}
