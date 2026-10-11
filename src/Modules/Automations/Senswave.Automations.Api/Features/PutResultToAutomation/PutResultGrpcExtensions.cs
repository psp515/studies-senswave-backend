using Senswave.Automations.Application.Features.PutResultToAutomation;
using Senswave.Automations.Domain.Entities;
using Senswave.Infrastructure.Grpc.Mapping;
using System.Text.Json.Nodes;
using Proto = Senswave.Contracts.Automations.V1;

namespace Senswave.Automations.Api.Features.PutResultToAutomation;

internal static class PutResultGrpcExtensions
{
    /// <summary>As in REST, the value is sent as a JSON string.</summary>
    internal static PutResultCommand ToCommand(this Proto.PutResultToAutomationRequest dto, Guid userId) => new()
    {
        AutomationId = ProtoMapping.ParseGuid(dto.AutomationId, "automationId"),
        UserId = userId,
        Result = new AutomationResult
        {
            OperationId = ProtoMapping.ParseGuid(dto.OperationId, "operationId"),
            ValueToSend = JsonValue.Create(dto.Value)
        }
    };
}
