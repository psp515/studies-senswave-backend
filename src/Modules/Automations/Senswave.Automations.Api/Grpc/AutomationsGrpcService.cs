using Grpc.Core;
using Senswave.Automations.Api.Features.CreateAutomation;
using Senswave.Automations.Api.Features.DeleteCondition;
using Senswave.Automations.Api.Features.GetAutomation;
using Senswave.Automations.Api.Features.PutConditionToAutomation;
using Senswave.Automations.Api.Features.PutResultToAutomation;
using Senswave.Automations.Api.Features.UpdateAutomation;
using Senswave.Automations.Application.Features.AutomationState;
using Senswave.Automations.Application.Features.DeleteAutomation;
using Senswave.Automations.Application.Features.DeleteResult;
using Senswave.Automations.Application.Features.DisplayAutomations;
using Senswave.Automations.Application.Features.GetAutomation;
using Senswave.Contracts.Common.V1;
using Senswave.Infrastructure.Grpc.Errors;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Automations.V1;

namespace Senswave.Automations.Api.Grpc;

[Authorize(AuthenticationSchemes = "Identity.Bearer")]
internal sealed class AutomationsGrpcService(IMediator mediator, IRequestContext requestContext)
    : Proto.AutomationsService.AutomationsServiceBase
{
    public override async Task<IdResponse> CreateAutomation(Proto.CreateAutomationRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new IdResponse { Id = result.Data.Id.ToString() };
    }

    public override async Task<Proto.UpdateAutomationResponse> UpdateAutomation(Proto.UpdateAutomationRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.UpdateAutomationResponse();
    }

    public override async Task<Proto.DeleteAutomationResponse> DeleteAutomation(Proto.DeleteAutomationRequest request, ServerCallContext context)
    {
        var command = new DeleteAutomationCommand
        {
            AutomationId = ProtoMapping.ParseGuid(request.AutomationId, "automationId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteAutomationResponse();
    }

    public override async Task<Proto.GetAutomationResponse> GetAutomation(Proto.GetAutomationRequest request, ServerCallContext context)
    {
        var query = new GetAutomationQuery
        {
            AutomationId = ProtoMapping.ParseGuid(request.AutomationId, "automationId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.GetAutomationResponse { Automation = result.Data.ToProto() };
    }

    public override async Task<Proto.DisplayAutomationsResponse> DisplayAutomations(Proto.DisplayAutomationsRequest request, ServerCallContext context)
    {
        var query = new DisplayAutomationsQuery
        {
            HomeId = ProtoMapping.ParseGuid(request.HomeId, "homeId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(query, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DisplayAutomationsResponse { Items = { result.Data.Select(x => x.ToProto()) } };
    }

    public override async Task<Proto.AutomationStateResponse> AutomationState(Proto.AutomationStateRequest request, ServerCallContext context)
    {
        var command = new AutomationStateCommand
        {
            AutomationId = ProtoMapping.ParseGuid(request.AutomationId, "automationId"),
            UserId = requestContext.UserId,
            IsEnabled = request.IsEnabled
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.AutomationStateResponse();
    }

    public override async Task<Proto.PutConditionToAutomationResponse> PutConditionToAutomation(
        Proto.PutConditionToAutomationRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.PutConditionToAutomationResponse();
    }

    public override async Task<Proto.PutResultToAutomationResponse> PutResultToAutomation(
        Proto.PutResultToAutomationRequest request, ServerCallContext context)
    {
        var result = await mediator.Send(request.ToCommand(requestContext.UserId), context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.PutResultToAutomationResponse();
    }

    public override async Task<Proto.DeleteConditionResponse> DeleteCondition(Proto.DeleteConditionRequest request, ServerCallContext context)
    {
        var command = ProtoMapping.ParseGuid(request.ConditionId, "conditionId").ToDeleteConditionCommand(requestContext.UserId);

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteConditionResponse();
    }

    public override async Task<Proto.DeleteResultResponse> DeleteResult(Proto.DeleteResultRequest request, ServerCallContext context)
    {
        var command = new DeleteResultCommand
        {
            ResultId = ProtoMapping.ParseGuid(request.ResultId, "resultId"),
            UserId = requestContext.UserId
        };

        var result = await mediator.Send(command, context.CancellationToken);
        result.ThrowIfFailure();

        return new Proto.DeleteResultResponse();
    }
}
