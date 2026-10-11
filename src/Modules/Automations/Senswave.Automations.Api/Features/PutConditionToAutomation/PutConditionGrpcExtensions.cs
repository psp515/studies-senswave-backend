using Senswave.Automations.Api.Features.CreateAutomation;
using Senswave.Automations.Application.Features.PutConditionToAutomation;
using Senswave.Automations.Domain.Entities;
using Senswave.Infrastructure.Grpc.Mapping;
using Proto = Senswave.Contracts.Automations.V1;

namespace Senswave.Automations.Api.Features.PutConditionToAutomation;

internal static class PutConditionGrpcExtensions
{
    internal static PutConditionCommand ToCommand(this Proto.PutConditionToAutomationRequest dto, Guid userId) => new()
    {
        AutomationId = ProtoMapping.ParseGuid(dto.AutomationId, "automationId"),
        UserId = userId,
        AutomationCondition = new AutomationCondition
        {
            OperationId = ProtoMapping.ParseGuid(dto.OperationId, "operationId"),
            ConditionType = dto.ConditionType.ToDomain(),
            ConditionConfiguration = dto.ConditionConfiguration.ToJsonObject("conditionConfiguration")
        }
    };
}
